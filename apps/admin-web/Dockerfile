FROM node:22-alpine AS build
RUN corepack enable
WORKDIR /app

# Copy workspace manifests first for layer caching
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml ./
COPY apps/admin-web/package.json apps/admin-web/

RUN pnpm install --frozen-lockfile --filter admin-web

COPY apps/admin-web/ apps/admin-web/

ARG BASE_HREF=/admin/
WORKDIR /app/apps/admin-web
RUN pnpm exec ng build --base-href=${BASE_HREF}

FROM nginx:alpine AS runtime
COPY --from=build /app/apps/admin-web/dist/admin-web/browser /usr/share/nginx/html
COPY --from=build /app/apps/admin-web/nginx.conf /etc/nginx/conf.d/default.conf
