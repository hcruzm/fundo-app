import type { Metadata } from "next";
import { Geist, Geist_Mono } from "next/font/google";
import "./globals.css";

import { ApplicationsPanel } from "@/components/applications-panel";
import { Toaster } from "@/components/ui/sonner";

const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin"],
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
});

export const metadata: Metadata = {
  title: "Fundo loan application",
  description: "Apply for a Fundo business loan and get an instant decision.",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html
      lang="en"
      className={`${geistSans.variable} ${geistMono.variable} h-full antialiased`}
    >
      <body className="min-h-full flex flex-col">
        <div className="flex min-h-screen flex-col lg:flex-row">
          <div className="flex min-w-0 flex-1 flex-col">{children}</div>
          <aside className="flex w-full shrink-0 flex-col border-t p-6 lg:sticky lg:top-0 lg:h-screen lg:w-96 lg:border-t-0 lg:border-l">
            <ApplicationsPanel />
          </aside>
        </div>
        <Toaster />
      </body>
    </html>
  );
}
