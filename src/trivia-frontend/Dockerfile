# syntax=docker/dockerfile:1

# Etapa 1: compila el microfrontend con la versión de Node de .nvmrc (Node.js 22).
FROM node:22-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY . .
# Webpack escribe la URL de la API dentro del bundle: se define al construir, no al ejecutar.
ARG TRIVIA_API_URL
ENV TRIVIA_API_URL=${TRIVIA_API_URL}
RUN npm run build

# Etapa 2: solo los archivos estáticos de dist/, servidos por nginx sin root.
FROM nginxinc/nginx-unprivileged:stable-alpine-slim
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 8080
