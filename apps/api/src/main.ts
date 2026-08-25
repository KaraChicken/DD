import Fastify from "fastify";
import cors from "@fastify/cors";
import { env } from "./config.js";
import { authRoutes } from "./routes/auth.js";

const app = Fastify({ logger: true });

await app.register(cors, { origin: true, credentials: true });
await app.register(authRoutes);

app.get("/health", async () => ({
  status: "ok",
  service: "dd-api",
  environment: env.NODE_ENV,
}));

await app.listen({ port: env.PORT, host: env.HOST });
