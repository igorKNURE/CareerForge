# Multi-stage build for the CareerForge frontend.
# Stage 1 produces the Vite bundle; stage 2 serves it via nginx.
#
# VITE_API_BASE is consumed at build time and inlined into the bundle. fly.toml supplies
# the production backend URL.

FROM node:20-alpine AS build
WORKDIR /app

ARG VITE_API_BASE
ENV VITE_API_BASE=${VITE_API_BASE}

# Restore is performed in its own layer so the package cache survives source changes.
# Optional dependencies are required: Rolldown's native bindings are resolved
# per-platform via optionalDependencies.
COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build

# ---

FROM nginx:1.27-alpine AS runtime
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 8080
