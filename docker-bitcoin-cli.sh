#!/bin/bash

docker exec -ti nldk_bitcoind bitcoin-cli -datadir="/data" "$@"
