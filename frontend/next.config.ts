import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // The dev server writes generated guide files into the project root on every
  // start. This project keeps its documentation in README.md and ARCHITECTURE.md.
  agentRules: false,
};

export default nextConfig;
