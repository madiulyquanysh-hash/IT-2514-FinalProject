#!/bin/sh
set -e
cd "$(dirname "$0")"
mkdir -p bin

case "$(uname -s)" in
    Darwin)
        cc -shared -fPIC -O2 -Wall -Wextra -std=c99 -o bin/libticketfees.dylib ticketfees.c
        echo "Built native/bin/libticketfees.dylib"
        ;;
    *)
        cc -shared -fPIC -O2 -Wall -Wextra -std=c99 -o bin/libticketfees.so ticketfees.c
        echo "Built native/bin/libticketfees.so"
        ;;
esac