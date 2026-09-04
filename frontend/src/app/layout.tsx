import type { Metadata } from "next";
import { Fraunces, Inter } from "next/font/google";
import { Suspense } from "react";
import { Toaster } from "sonner";
import { Providers } from "@/components/providers";
import { LoadingBar } from "@/features/shell/loading-bar";
import { THEME_BOOT_SCRIPT } from "@/lib/theme";
import "./globals.css";

const inter = Inter({
  variable: "--font-sans",
  subsets: ["latin"],
});

// A distinct display face for the brand wordmark/tagline (client request,
// 2026-09-05) — everything else in the app stays on Inter; this only feeds
// the `font-heading` utility, already scaffolded in globals.css but unused
// until now.
const fraunces = Fraunces({
  variable: "--font-display",
  subsets: ["latin"],
  weight: ["500", "600"],
  style: ["normal", "italic"],
});

export const metadata: Metadata = {
  title: "Colour Bricks",
  description: "Construction project financial management",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html
      lang="en"
      className={`${inter.variable} ${fraunces.variable} h-full antialiased`}
      suppressHydrationWarning
    >
      <head>
        {/* Applies the stored light/dark mode and accent before first paint, so
            there's no flash of the wrong theme (client request, 2026-09-04). */}
        <script dangerouslySetInnerHTML={{ __html: THEME_BOOT_SCRIPT }} />
      </head>
      <body className="bg-background text-foreground flex min-h-full flex-col">
        {/* Loading bar for page navigation + API calls (client request, 2026-09-04). */}
        <Suspense fallback={null}>
          <LoadingBar />
        </Suspense>
        <Providers>{children}</Providers>
        <Toaster position="top-right" richColors closeButton />
      </body>
    </html>
  );
}
