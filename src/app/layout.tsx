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
  openGraph: {
    title: "アバター試着データベース",
    description: "VRChatアバターの衣装データベース",
    type: "website",
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
