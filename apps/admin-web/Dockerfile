FROM node:22-alpine AS build
RUN corepack enable
WORKDIR /app
COPY package.json pnpm-lock.yaml ./
RUN pnpm install --frozen-lockfile
COPY . .
ARG BASE_HREF=/admin/
RUN pnpm build -- --base-href=${BASE_HREF}

FROM nginx:alpine AS runtime
COPY --from=build /app/dist/admin-web/browser /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
