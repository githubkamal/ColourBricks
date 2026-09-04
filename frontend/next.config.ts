import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // Dev-only cosmetic badge (bottom-left "N" button) — not part of the app,
  // just distracting during manual testing/screenshots. No effect in production.
  devIndicators: false,
};

export default nextConfig;
