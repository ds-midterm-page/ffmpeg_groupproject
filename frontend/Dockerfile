# Stage 1: Build the frontend app
FROM node:20-alpine AS build
WORKDIR /app

# Copy package files and install dependencies
COPY package*.json ./
RUN npm install

# Copy source code and build the static assets (creates /app/dist or /app/build)
COPY . .
RUN npm run build

# Stage 2: Serve the built app using Nginx
FROM nginx:alpine

# Copy built static files from Stage 1 into Nginx HTML directory
# Note: If your framework outputs to /app/build instead of /app/dist, change 'dist' to 'build' below.
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]