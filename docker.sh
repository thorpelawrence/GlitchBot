#!/bin/bash
docker stop glitchbot
docker rm glitchbot
docker rmi glitchbot
docker build -t glitchbot .
docker run -d --name glitchbot glitchbot
