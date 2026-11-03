# Dockerfile cho frontend Culinary Blog (mục 11) — multi-stage build để image production nhỏ gọn.
# Build:  docker build -t culinary-blog-frontend .
# Run:    docker run -p 3000:3000 --env-file .env.local culinary-blog-frontend

# ---- Stage 1: cài dependency ----
FROM node:20-alpine AS deps
WORKDIR /app
COPY package.json package-lock.json* ./
RUN npm ci

# ---- Stage 2: build ----
FROM node:20-alpine AS builder
WORKDIR /app
COPY --from=deps /app/node_modules ./node_modules
COPY . .
# Các biến NEXT_PUBLIC_* phải có mặt LÚC BUILD (Next.js inline chúng vào bundle client).
ARG NEXT_PUBLIC_API_BASE_URL
ENV NEXT_PUBLIC_API_BASE_URL=$NEXT_PUBLIC_API_BASE_URL
# Không chạy prebuild/setup-env: secret chỉ được cấp khi khởi động container.
RUN node node_modules/next/dist/bin/next build

# ---- Stage 3: chạy production ----
# next.config.ts đã bật `output: "standalone"` nên chỉ cần copy đúng 3 thứ dưới đây,
# không cần copy node_modules hay source gốc.
FROM node:20-alpine AS runner
WORKDIR /app
ENV NODE_ENV=production

RUN addgroup --system --gid 1001 nodejs && adduser --system --uid 1001 nextjs

COPY --from=builder /app/public ./public
COPY --from=builder --chown=nextjs:nodejs /app/.next/standalone ./
COPY --from=builder --chown=nextjs:nodejs /app/.next/static ./.next/static

USER nextjs
EXPOSE 3000
ENV PORT=3000

CMD ["node", "server.js"]
