import type { Metadata } from "next";
import localFont from "next/font/local";
import { Suspense } from "react";
import { Toaster } from "sonner";
import { Providers } from "@/components/providers";
import { LoadingBar } from "@/features/shell/loading-bar";
import { apiBaseUrl } from "@/lib/config";
import { THEME_BOOT_SCRIPT } from "@/lib/theme";
import "./globals.css";

// Every page issues a credentialed cross-origin call to this origin (at minimum
// the auth check on load), so warm the connection before it's needed instead of
// paying DNS + TCP + TLS after the fact (GTmetrix: "Preconnect to required origins").
const apiOrigin = new URL(apiBaseUrl).origin;

// The Adminto theme's body/heading pairing (client request, 2026-09-06). Self-hosted
// variable fonts (OFL, from github.com/google/fonts) rather than next/font/google:
// Turbopack fails the build whenever Google Fonts answers with an extensionless
// `/l/font?kit=…&skey=…` URL (vercel/next.js#99114), so the build must not fetch them.
const publicSans = localFont({
  src: "./fonts/PublicSans-Variable.woff2",
  variable: "--font-sans",
  weight: "100 900",
  display: "swap",
});

const outfit = localFont({
  src: "./fonts/Outfit-Variable.woff2",
  variable: "--font-display",
  weight: "100 900",
  display: "swap",
});

export const metadata: Metadata = {
  title: "Colour Bricks",
  description: "Construction project financial management",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html
      lang="en"
      className={`${publicSans.variable} ${outfit.variable} h-full antialiased`}
      suppressHydrationWarning
    >
      <head>
        <link rel="preconnect" href={apiOrigin} crossOrigin="use-credentials" />
        <link rel="dns-prefetch" href={apiOrigin} />
        {/* Matches the light-mode background; the boot script/theme toggle update this
            to the dark background before/after a mode switch. */}
        <meta name="theme-color" content="#f0f4f7" />
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
