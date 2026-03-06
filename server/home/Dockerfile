FROM node:22-alpine AS deps
WORKDIR /app
COPY package.json package-lock.json* .npmrc* ./
RUN npm install

FROM node:22-alpine AS build
WORKDIR /app
ARG VITE_API_BASE_URL=https://api.xn--pcwchter-2za.de/api/v1
ARG VITE_KEYCLOAK_URL=https://login.xn--pcwchter-2za.de
ARG VITE_KEYCLOAK_REALM=pcwaechter-prod
ARG VITE_KEYCLOAK_CLIENT_ID=home-web
ARG VITE_PREVIEW_MODE=false
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL \
    VITE_KEYCLOAK_URL=$VITE_KEYCLOAK_URL \
    VITE_KEYCLOAK_REALM=$VITE_KEYCLOAK_REALM \
    VITE_KEYCLOAK_CLIENT_ID=$VITE_KEYCLOAK_CLIENT_ID \
    VITE_PREVIEW_MODE=$VITE_PREVIEW_MODE
COPY --from=deps /app/node_modules ./node_modules
COPY . .
RUN npm run build

FROM node:22-alpine AS runner
WORKDIR /app
ENV NODE_ENV=production
RUN npm install -g serve@14.2.4
COPY --from=build /app/dist ./dist
EXPOSE 3000
CMD ["serve", "-s", "dist", "-l", "3000"]
