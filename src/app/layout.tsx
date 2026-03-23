import type { Metadata } from "next";
import { Noto_Sans_JP } from "next/font/google";
import "./globals.css";

const notoSansJP = Noto_Sans_JP({
  variable: "--font-noto-sans-jp",
  subsets: ["latin"],
  weight: ["400", "500", "700"],
});

export const metadata: Metadata = {
  title: "アバター試着データベース | VRChat衣装プレビュー",
  description:
    "VRChatアバターの衣装データベース。人気アバターの対応衣装をブラウザでプレビュー。購入前にどう見えるかチェックしよう。",
  keywords: [
    "VRChat",
    "アバター",
    "衣装",
    "試着",
    "プレビュー",
    "Booth",
    "VRC",
    "対応衣装",
    "フィッティング",
  ],
  openGraph: {
    title: "アバター試着データベース | VRChat衣装プレビュー",
    description:
      "VRChatアバターの衣装データベース。購入前にアバター×衣装の組み合わせをブラウザでプレビュー。",
    type: "website",
    locale: "ja_JP",
    siteName: "アバター試着データベース",
  },
  twitter: {
    card: "summary_large_image",
    title: "アバター試着データベース | VRChat衣装プレビュー",
    description:
      "VRChatアバターの対応衣装をブラウザでプレビュー。購入前にどう見えるかチェック。",
  },
  robots: {
    index: true,
    follow: true,
  },
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="ja" className={`${notoSansJP.variable} h-full antialiased`}>
      <body className="min-h-full flex flex-col text-gray-700 font-[var(--font-noto-sans-jp)]">
        {children}
      </body>
    </html>
  );
}
