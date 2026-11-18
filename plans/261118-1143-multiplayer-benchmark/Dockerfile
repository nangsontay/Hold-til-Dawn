FROM node:22-bookworm-slim
RUN apt-get update && apt-get install -y --no-install-recommends iproute2 ethtool libfontconfig1 libx11-6 libxcursor1 libxinerama1 libxi6 libxrandr2 libxrender1 libasound2 libpulse0 libgl1 libudev1 ca-certificates && rm -rf /var/lib/apt/lists/*
WORKDIR /bench
COPY colyseus/package*.json /bench/colyseus/
RUN cd colyseus && npm ci --omit=dev
