# The CRM front end.
#
# Next.js is compiled at build time, so the API URL it talks to is baked in
# here rather than read at runtime — NEXT_PUBLIC_* values are inlined into the
# browser bundle by definition. Change the URL, rebuild the image.

FROM node:22-alpine AS deps
WORKDIR /app
COPY package.json package-lock.json* ./
RUN npm ci

FROM node:22-alpine AS build
WORKDIR /app
COPY --from=deps /app/node_modules ./node_modules
COPY . .

ARG NEXT_PUBLIC_API_URL
ENV NEXT_PUBLIC_API_URL=$NEXT_PUBLIC_API_URL
ENV NEXT_TELEMETRY_DISABLED=1

RUN npm run build

FROM node:22-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production
ENV NEXT_TELEMETRY_DISABLED=1

# The standalone bundle carries its own minimal node_modules; static/ and
# public/ sit outside it and have to be copied in alongside.
COPY --from=build /app/.next/standalone ./
COPY --from=build /app/.next/static ./.next/static
COPY --from=build /app/public ./public

RUN addgroup -g 10001 web && adduser -u 10001 -G web -S web \
    && chown -R web:web /app
USER web

EXPOSE 3000
ENV PORT=3000
ENV HOSTNAME=0.0.0.0

CMD ["node", "server.js"]
