"use client";

import { useEffect, useState } from "react";

interface BoothItem {
  title: string;
  link: string;
  boothId: string;
  category: string;
  price: string;
  creator: string;
  fetchedAt: string;
}

interface NewArrivalsData {
  lastChecked: string;
  avatars: BoothItem[];
  outfits: BoothItem[];
}

export function NewArrivals() {
  const [data, setData] = useState<NewArrivalsData | null>(null);
  const [tab, setTab] = useState<"avatar" | "outfit">("avatar");

  useEffect(() => {
    fetch("/data/new-arrivals.json")
      .then((res) => (res.ok ? res.json() : null))
      .then(setData)
      .catch(() => setData(null));
  }, []);

  if (!data) return null;

  const items = tab === "avatar" ? data.avatars : data.outfits;
  const lastChecked = new Date(data.lastChecked).toLocaleDateString("ja-JP", {
    month: "long",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });

  return (
    <section className="mt-8 bg-white/80 backdrop-blur-sm rounded-2xl border border-pink-100 p-5 shadow-sm">
      {/* ヘッダー */}
      <div className="flex items-center justify-between mb-4">
        <h2 className="text-base font-bold text-gray-600 flex items-center gap-2">
          <span>✨</span>
          <span>Booth新着情報</span>
        </h2>
        <span className="text-xs text-gray-400">最終更新: {lastChecked}</span>
      </div>

      {/* タブ */}
      <div className="flex gap-2 mb-4">
        <button
          onClick={() => setTab("avatar")}
          className={`px-4 py-1.5 rounded-full text-xs font-medium transition-all ${
            tab === "avatar"
              ? "bg-pink-400 text-white"
              : "bg-pink-50 text-pink-400 hover:bg-pink-100"
          }`}
        >
          アバター ({data.avatars.length})
        </button>
        <button
          onClick={() => setTab("outfit")}
          className={`px-4 py-1.5 rounded-full text-xs font-medium transition-all ${
            tab === "outfit"
              ? "bg-sky-300 text-white"
              : "bg-sky-50 text-sky-400 hover:bg-sky-100"
          }`}
        >
          衣装 ({data.outfits.length})
        </button>
      </div>

      {/* アイテムリスト */}
      <div className="grid grid-cols-2 md:grid-cols-4 lg:grid-cols-5 gap-3">
        {items.slice(0, 10).map((item) => (
          <a
            key={item.boothId}
            href={item.link}
            target="_blank"
            rel="noopener noreferrer"
            className="group block rounded-xl bg-gradient-to-b from-sky-50/50 to-pink-50/50 border border-pink-100 p-3 hover:border-pink-300 hover:shadow-md transition-all"
          >
            <div className="text-xs font-medium text-gray-600 line-clamp-2 group-hover:text-pink-500 transition-colors">
              {item.title}
            </div>
            <div className="mt-1 text-xs text-pink-400">
              {item.price || "価格未取得"}
            </div>
            <div className="mt-1 text-[10px] text-gray-400">
              #{item.boothId}
            </div>
          </a>
        ))}
      </div>
    </section>
  );
}
