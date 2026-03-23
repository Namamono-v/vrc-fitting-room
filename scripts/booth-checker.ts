/**
 * Booth新着チェッカー
 * Booth検索ページから VRChat アバター・衣装の新着を取得し、JSONで保存する
 *
 * 使い方: npx tsx scripts/booth-checker.ts
 */

import * as fs from "fs";
import * as path from "path";

interface BoothItem {
  title: string;
  link: string;
  boothId: string;
  category: string;
  price: string;
  creator: string;
  fetchedAt: string;
}

interface NewArrivals {
  lastChecked: string;
  avatars: BoothItem[];
  outfits: BoothItem[];
}

const SEARCHES = [
  {
    url: "https://booth.pm/ja/search/VRChat%20%E3%82%A2%E3%83%90%E3%82%BF%E3%83%BC?sort=new",
    type: "avatar" as const,
    label: "VRChat アバター",
  },
  {
    url: "https://booth.pm/ja/search/VRChat%20%E8%A1%A3%E8%A3%85?sort=new",
    type: "outfit" as const,
    label: "VRChat 衣装",
  },
];

function parseItems(html: string, type: "avatar" | "outfit"): BoothItem[] {
  const items: BoothItem[] = [];

  // Booth の商品カードを正規表現で抽出
  // 商品リンク: /ja/items/XXXXXXX
  const itemPattern = /href="(\/ja\/items\/(\d+))"[^>]*>[\s\S]*?<div[^>]*class="[^"]*item-card__title[^"]*"[^>]*>([\s\S]*?)<\/div>/g;
  let match;

  while ((match = itemPattern.exec(html)) !== null) {
    const link = `https://booth.pm${match[1]}`;
    const boothId = match[2];
    const title = match[3].replace(/<[^>]+>/g, "").trim();

    items.push({
      title,
      link,
      boothId,
      category: type === "avatar" ? "アバター" : "衣装",
      price: "",
      creator: "",
      fetchedAt: new Date().toISOString(),
    });
  }

  // フォールバック: もっとシンプルなパターンで試す
  if (items.length === 0) {
    const simplePattern = /\/ja\/items\/(\d+)/g;
    const seen = new Set<string>();
    let m;
    while ((m = simplePattern.exec(html)) !== null) {
      const id = m[1];
      if (seen.has(id)) continue;
      seen.add(id);
      items.push({
        title: `Booth商品 #${id}`,
        link: `https://booth.pm/ja/items/${id}`,
        boothId: id,
        category: type === "avatar" ? "アバター" : "衣装",
        price: "",
        creator: "",
        fetchedAt: new Date().toISOString(),
      });
    }
  }

  return items.slice(0, 20); // 最新20件
}

async function fetchPage(url: string): Promise<string> {
  const res = await fetch(url, {
    headers: {
      "User-Agent":
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
      Accept: "text/html",
      "Accept-Language": "ja,en;q=0.5",
    },
  });
  if (!res.ok) throw new Error(`HTTP ${res.status}: ${url}`);
  return res.text();
}

async function main() {
  console.log("=== Booth 新着チェッカー ===");
  console.log(`実行時刻: ${new Date().toLocaleString("ja-JP")}\n`);

  const result: NewArrivals = {
    lastChecked: new Date().toISOString(),
    avatars: [],
    outfits: [],
  };

  for (const search of SEARCHES) {
    console.log(`取得中: ${search.label}...`);
    try {
      const html = await fetchPage(search.url);
      const items = parseItems(html, search.type);
      console.log(`  → ${items.length}件取得`);

      if (search.type === "avatar") {
        result.avatars = items;
      } else {
        result.outfits = items;
      }
    } catch (e: any) {
      console.error(`  → エラー: ${e.message}`);
    }
  }

  // 結果表示
  console.log("\n--- 新着アバター TOP5 ---");
  for (const item of result.avatars.slice(0, 5)) {
    console.log(`  ${item.title}`);
    console.log(`    ${item.link}`);
  }

  console.log("\n--- 新着衣装 TOP5 ---");
  for (const item of result.outfits.slice(0, 5)) {
    console.log(`  ${item.title}`);
    console.log(`    ${item.link}`);
  }

  // JSONで保存
  const outPath = path.join(__dirname, "..", "public", "data", "new-arrivals.json");
  const outDir = path.dirname(outPath);
  if (!fs.existsSync(outDir)) fs.mkdirSync(outDir, { recursive: true });

  fs.writeFileSync(outPath, JSON.stringify(result, null, 2), "utf-8");
  console.log(`\n保存完了: ${outPath}`);
  console.log(`アバター: ${result.avatars.length}件 / 衣装: ${result.outfits.length}件`);
}

main().catch(console.error);
