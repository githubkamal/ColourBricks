import type { Metadata } from "next";
import { Outfit, Public_Sans } from "next/font/google";
import { Suspense } from "react";
import { Toaster } from "sonner";
import { Providers } from "@/components/providers";
import { LoadingBar } from "@/features/shell/loading-bar";
import { THEME_BOOT_SCRIPT } from "@/lib/theme";
import "./globals.css";

// The Adminto theme's body/heading pairing (client request, 2026-09-06).
const publicSans = Public_Sans({
  variable: "--font-sans",
  subsets: ["latin"],
});

const outfit = Outfit({
  variable: "--font-display",
  subsets: ["latin"],
  weight: ["500", "600", "700"],
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
