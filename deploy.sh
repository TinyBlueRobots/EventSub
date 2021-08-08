#!/bin/bash
./build.sh
docker build -t tinybluerobots/eventsub:latest .
docker push tinybluerobots/eventsub:latest