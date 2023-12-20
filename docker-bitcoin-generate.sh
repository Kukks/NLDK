#!/bin/bash
address=$(docker exec -ti nldk_bitcoind bitcoin-cli -datadir="/data" getnewaddress)
clean_address="${address//[$'\t\r\n']}"
docker exec nldk_bitcoind bitcoin-cli -datadir="/data" generatetoaddress "$@" "$clean_address"
