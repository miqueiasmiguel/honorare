FROM node:22-alpine AS build
RUN corepack enable
WORKDIR /app

# Copy workspace manifests first for layer caching
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml ./
COPY apps/medico-pwa/package.json apps/medico-pwa/

RUN pnpm install --frozen-lockfile --filter medico-pwa

COPY apps/medico-pwa/ apps/medico-pwa/

ARG BASE_HREF=/app/
WORKDIR /app/apps/medico-pwa
RUN pnpm exec ng build --base-href=${BASE_HREF}

FROM nginx:alpine AS runtime
COPY --from=build /app/apps/medico-pwa/dist/medico-pwa/browser /usr/share/nginx/html
COPY --from=build /app/apps/medico-pwa/nginx.conf /etc/nginx/conf.d/default.conf
