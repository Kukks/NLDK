$address=$(docker exec -ti nldk_bitcoind bitcoin-cli -datadir="/data" getnewaddress)
docker exec -ti nldk_bitcoind bitcoin-cli -datadir="/data" generatetoaddress $args $address
