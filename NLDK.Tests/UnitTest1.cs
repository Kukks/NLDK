using System.Text;
using BetterTesting.Tests;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.Logging.Console;
using NBitcoin;
using NBXplorer;
using NBXplorer.Models;
using NLDK;
using nldksample.LDK;
using Xunit.Abstractions;


public class UnitTest1
{
    private readonly ITestOutputHelper _testOutputHelper;

    public UnitTest1(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public async Task Test1()
    {
        await using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>().WithTestLogging(_testOutputHelper).WithWebHostBuilder(
            builder =>
            {
                builder.ConfigureAppConfiguration(configurationBuilder =>
                {
                    configurationBuilder.Add(
                        new MemoryConfigurationSource
                        {
                            InitialData = new Dictionary<string, string?>(StringComparer.InvariantCultureIgnoreCase)
                            {
                                {"ConnectionString", $"Data Source={Guid.NewGuid()}.db"},
                                {"NBXplorerConnection","http://localhost:24446"}
                            }
                        });
                });
            });


        var explorerClient = factory.Services.GetRequiredService<ExplorerClient>();
        var walletService = factory.Services.GetRequiredService<WalletService>();
        var nodeManager = factory.Services.GetRequiredService<LDKNodeManager>();

        var wallet1 = await CreateWallet(factory.Services);
        var wallet2 = await CreateWallet(factory.Services);

        var wallet1Node = await nodeManager.GetLDKNodeForWallet(wallet1);
        var wallet2Node = await nodeManager.GetLDKNodeForWallet(wallet2);

        var wallet1Script = await walletService.DeriveScript(wallet1);
        var wallet2Script = await walletService.DeriveScript(wallet1);
        await explorerClient.RPCClient.GenerateAsync(2);
        await explorerClient.RPCClient.SendToAddressAsync(wallet1Script.ToScript(), Money.Coins(1));
        await explorerClient.RPCClient.SendToAddressAsync(wallet2Script.ToScript(), Money.Coins(2));
        await explorerClient.RPCClient.GenerateAsync(1);


        while (wallet1Node.NodeInfo is null || wallet2Node.NodeInfo is null)
        {
            await Task.Delay(100);
        }

        var wallet1PeerHandler = wallet1Node.ServiceProvider.GetRequiredService<LDKPeerHandler>();
        var wallet2PeerHandler = wallet2Node.ServiceProvider.GetRequiredService<LDKPeerHandler>();
        LDKSTcpDescriptor? wallet1Peer = null;
        
        while(wallet1Peer is null)
        {
            wallet1Peer = await wallet1PeerHandler.ConnectAsync(wallet2Node.NodeInfo);
        }
        
    }


    private async Task<string> CreateWallet(IServiceProvider serviceProvider)
    {
        var explorerClient = serviceProvider.GetRequiredService<ExplorerClient>();
        var walletService = serviceProvider.GetRequiredService<WalletService>();

        var genWallet = await explorerClient.GenerateWalletAsync();
        var hash = await explorerClient.RPCClient.GetBestBlockHashAsync();
        var ts = new DerivationSchemeTrackedSource(genWallet.DerivationScheme);
        var idx = genWallet.GetMnemonic().DeriveExtKey().GetPublicKey().GetHDFingerPrint().ToString();
        var wts = new GroupTrackedSource(idx);
        await explorerClient.TrackAsync(wts);
        await explorerClient.AddGroupChildrenAsync(wts.GroupId, new[] {new GroupChild()
        {
            TrackedSource = ts.ToString(),
            CryptoCode = "BTC"
        }});
        return await walletService.Create(genWallet.GetMnemonic(), null, genWallet.AccountKeyPath.KeyPath + "/*",
            hash.ToString(), new[] {ts.ToString(), wts.ToString()});
    }
}

// Copyright (c) Daniel Crenna. All rights reserved.
// 
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

namespace BetterTesting.Tests
{
    internal static class WebApplicationFactoryExtensions
    {
        #region Test Logging

        /// <summary>
        /// Redirect all application logging to the test console.
        /// </summary>
        /// <typeparam name="TStartup">The web application under test</typeparam>
        /// <param name="factory">The chained factory to enable test logging on</param>
        /// <param name="output">The current instance of the Xunit test output helper</param>
        /// <returns></returns>
        public static WebApplicationFactory<TStartup> WithTestLogging<TStartup>(this WebApplicationFactory<TStartup> factory, ITestOutputHelper output) where TStartup : class
        {
            return factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureLogging(logging =>
                {
                    // check if scopes are used in normal operation
                    var useScopes = logging.UsesScopes();

                    // remove other logging providers, such as remote loggers or unnecessary event logs
                    logging.ClearProviders();

                    logging.Services.AddSingleton<ILoggerProvider>(r => new XunitLoggerProvider(output, useScopes));
                });
            });
        }

        private sealed class XunitLogger : ILogger
        {
            private const string ScopeDelimiter = "=> ";
            private const string Spacer = "      ";

            private const string Trace = "trce";
            private const string Debug = "dbug";
            private const string Info = "info";
            private const string Warn = "warn";
            private const string Error = "fail";
            private const string Critical = "crit";
            
            private readonly string _categoryName;
            private readonly bool _useScopes;
            private readonly ITestOutputHelper _output;
            private readonly IExternalScopeProvider _scopes;

            public XunitLogger(ITestOutputHelper output, IExternalScopeProvider scopes, string categoryName, bool useScopes)
            {
                _output = output;
                _scopes = scopes;
                _categoryName = categoryName;
                _useScopes = useScopes;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return logLevel != LogLevel.None;
            }

            public IDisposable BeginScope<TState>(TState state)
            {
                return _scopes.Push(state);
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
                Func<TState, Exception, string> formatter)
            {
                var sb = new StringBuilder();

                switch (logLevel)
                {
                    case LogLevel.Trace:
                        sb.Append(Trace);
                        break;
                    case LogLevel.Debug:
                        sb.Append(Debug);
                        break;
                    case LogLevel.Information:
                        sb.Append(Info);
                        break;
                    case LogLevel.Warning:
                        sb.Append(Warn);
                        break;
                    case LogLevel.Error:
                        sb.Append(Error);
                        break;
                    case LogLevel.Critical:
                        sb.Append(Critical);
                        break;
                    case LogLevel.None:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null);
                }

                sb.Append(": ").Append(_categoryName).Append('[').Append(eventId).Append(']').AppendLine();

                if (_useScopes && TryAppendScopes(sb))
                    sb.AppendLine();

                sb.Append(Spacer);
                sb.Append(formatter(state, exception));

                if (exception != null)
                {
                    sb.AppendLine();
                    sb.Append(Spacer);
                    sb.Append(exception);
                }

                var message = sb.ToString();
                _output.WriteLine(message);
            }

            private bool TryAppendScopes(StringBuilder sb)
            {
                var scopes = false;
                _scopes.ForEachScope((callback, state) =>
                {
                    if (!scopes)
                    {
                        state.Append(Spacer);
                        scopes = true;
                    }

                    state.Append(ScopeDelimiter);
                    state.Append(callback);
                }, sb);
                return scopes;
            }
        }

        private sealed class XunitLoggerProvider : ILoggerProvider, ISupportExternalScope
        {
            private readonly ITestOutputHelper _output;
            private readonly bool _useScopes;

            private IExternalScopeProvider _scopes;

            public XunitLoggerProvider(ITestOutputHelper output, bool useScopes)
            {
                _output = output;
                _useScopes = useScopes;
            }

            public ILogger CreateLogger(string categoryName)
            {
                return new XunitLogger(_output, _scopes, categoryName, _useScopes);
            }

            public void Dispose()
            {
            }

            public void SetScopeProvider(IExternalScopeProvider scopes)
            {
                _scopes = scopes;
            }
        }

        private static bool UsesScopes(this ILoggingBuilder builder)
        {
            var serviceProvider = builder.Services.BuildServiceProvider();

            // look for other host builders on this chain calling ConfigureLogging explicitly
            var options = serviceProvider.GetService<SimpleConsoleFormatterOptions>() ??
                          serviceProvider.GetService<JsonConsoleFormatterOptions>() ??
                          serviceProvider.GetService<ConsoleFormatterOptions>();

            if (options != default)
                return options.IncludeScopes;

            // look for other configuration sources
            // See: https://docs.microsoft.com/en-us/dotnet/core/extensions/logging?tabs=command-line#set-log-level-by-command-line-environment-variables-and-other-configuration

            var config = serviceProvider.GetService<IConfigurationRoot>() ?? serviceProvider.GetService<IConfiguration>();
            var logging = config?.GetSection("Logging");
            if (logging == default)
                return false;

            var includeScopes = logging?.GetValue("Console:IncludeScopes", false);
            if (!includeScopes.Value)
                includeScopes = logging?.GetValue("IncludeScopes", false);

            return includeScopes.GetValueOrDefault(false);
        }

        #endregion
    }
}