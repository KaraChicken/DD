import Fastify from "fastify";
import cors from "@fastify/cors";
import { env } from "./config.js";

const app = Fastify({ logger: true });

await app.register(cors, { origin: true });

app.get("/health", async () => ({
  status: "ok",
  service: "dd-api",
  environment: env.NODE_ENV,
}));

await app.listen({ port: env.PORT, host: env.HOST });
