# Build from the repo root:  docker build -f frontend/Dockerfile .
FROM node:22-alpine AS build
WORKDIR /app

COPY frontend/package*.json ./
RUN npm ci

COPY frontend/ ./
RUN npm run build

# Use the selected nginx line on Alpine 3.24. A normal `docker compose up --build` may reuse a local
# base-image manifest; use `docker compose build --pull` when a fresh upstream manifest is required.
FROM nginx:1.31-alpine AS runtime

# Refresh the vulnerable runtime libraries from Alpine's signed repositories. These lower bounds
# include the fixes for the OpenSSL 3.5.7-r0 and libexpat 2.8.2-r0 findings while allowing later
# security revisions.
RUN apk add --no-cache --upgrade \
      "libcrypto3>=3.5.8-r0" \
      "libssl3>=3.5.8-r0" \
      "libexpat>=2.8.4-r0"

# nginx serves the built SPA and proxies /api to the api container (see nginx.conf).
COPY frontend/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 8080

# 127.0.0.1, not localhost: nginx binds IPv4 and localhost can resolve to ::1 first in the container.
HEALTHCHECK --interval=15s --timeout=5s --start-period=10s --retries=5 \
  CMD wget -qO- http://127.0.0.1:8080/healthz || exit 1
