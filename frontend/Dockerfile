# syntax=docker/dockerfile:1

# ---------- Build ----------
FROM node:26-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY . .
RUN npm run build

# ---------- Runtime: unprivileged nginx serving the SPA and proxying /api ----------
FROM nginxinc/nginx-unprivileged:1.29-alpine
ENV API_UPSTREAM=http://api:8080
COPY nginx/default.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 8080
