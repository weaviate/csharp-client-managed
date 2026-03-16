# Build stage
FROM node:18-alpine AS build
WORKDIR /app

# Copy package files
COPY weaviate/csharp-client-managed/src/Example/WebApp/package*.json ./

# Install dependencies
RUN npm ci

# Copy source code
COPY weaviate/csharp-client-managed/src/Example/WebApp/ ./

# Build the app
RUN npm run build

# Runtime stage
FROM node:18-alpine AS runtime
WORKDIR /app

# Copy built app
COPY --from=build /app/build ./build
COPY --from=build /app/package*.json ./

# Install production dependencies only
RUN npm ci --omit=dev

# Expose port
EXPOSE 5174

# Set environment
ENV NODE_ENV=production
ENV PORT=5174
ENV HOST=0.0.0.0

# Start the app
CMD ["node", "build"]
