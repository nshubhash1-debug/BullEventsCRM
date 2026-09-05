import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // Bundles the server and only the dependencies it actually imports into
  // .next/standalone, so the runtime image ships without node_modules — a few
  // hundred megabytes smaller, and nothing unused sitting on a public box.
  output: "standalone",
};

export default nextConfig;
