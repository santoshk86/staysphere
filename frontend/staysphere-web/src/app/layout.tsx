import type { Metadata } from "next";
import { Geist, Geist_Mono } from "next/font/google";
import { SiteHeader } from "@/components/SiteHeader";
import "./globals.css";

const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin"],
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
});

export const metadata: Metadata = {
  title: {
    default: "StaySphere — Hotel room booking",
    template: "%s · StaySphere",
  },
  description: "Search availability and book a hotel room in a few steps.",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="en" className={`${geistSans.variable} ${geistMono.variable} h-full`}>
      <body className="flex min-h-full flex-col">
        <SiteHeader />
        <main className="mx-auto w-full max-w-5xl flex-1 px-4 py-8 sm:px-6">
          {children}
        </main>
        <footer className="border-t border-border bg-surface">
          <div className="mx-auto max-w-5xl px-4 py-6 text-xs text-muted sm:px-6">
            StaySphere is a demo booking experience. No payment is taken.
          </div>
        </footer>
      </body>
    </html>
  );
}
