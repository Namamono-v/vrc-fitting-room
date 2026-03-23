/**
 * Booth人気アイテム取得 & サムネイルダウンロード
 * 負荷軽減のため2秒間隔でリクエスト
 */
const https = require("https");
const http = require("http");
const fs = require("fs");
const path = require("path");

const DELAY_MS = 2000;
const AVATAR_COUNT = 50;
const OUTFIT_COUNT = 100;
const ITEMS_PER_PAGE = 24; // Boothの1ページあたり

const AVATAR_DIR = path.join(__dirname, "../public/thumbnails/avatars");
const OUTFIT_DIR = path.join(__dirname, "../public/thumbnails/outfits");
const DATA_DIR = path.join(__dirname, "../src/data");

function sleep(ms) {
  return new Promise((r) => setTimeout(r, ms));
}

function fetch(url) {
  return new Promise((resolve, reject) => {
    const mod = url.startsWith("https") ? https : http;
    const req = mod.get(url, { headers: { "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36" } }, (res) => {
      if (res.statusCode >= 300 && res.statusCode < 400 && res.headers.location) {
        return fetch(res.headers.location).then(resolve).catch(reject);
      }
      const chunks = [];
      res.on("data", (c) => chunks.push(c));
      res.on("end", () => resolve({ status: res.statusCode, body: Buffer.concat(chunks) }));
    });
    req.on("error", reject);
    req.setTimeout(15000, () => { req.destroy(); reject(new Error("timeout")); });
  });
}

async function fetchBoothPage(category, page) {
  // category: "3Dキャラクター" or "3D衣装"
  const encoded = encodeURIComponent(category);
  const url = `https://booth.pm/ja/search/${encoded}?sort=wish_lists&page=${page}`;
  console.log(`  GET ${url}`);
  const res = await fetch(url);
  const html = res.body.toString("utf-8");
  return html;
}

function parseItems(html) {
  const items = [];
  // アイテムカード from search results
  // data-product-id="XXXXX" pattern
  const idRegex = /data-product-id="(\d+)"/g;
  const ids = [];
  let m;
  while ((m = idRegex.exec(html)) !== null) {
    if (!ids.includes(m[1])) ids.push(m[1]);
  }

  // 画像URL: lazy loading img with src containing booth.pximg.net
  // Pattern: <img ... src="https://booth.pximg.net/.../{itemId}/...jpg"
  for (const id of ids) {
    // Find image URL for this item ID
    const imgRegex = new RegExp(
      `(https://booth\\.pximg\\.net/[^"]*?/i/${id}/[^"]*?_base_resized\\.[a-z]+)`,
      "i"
    );
    const imgMatch = html.match(imgRegex);

    // Find item name - look for the item link text
    // Pattern: /ja/items/{id}" ...>{name}</a>
    const nameRegex = new RegExp(
      `/ja/items/${id}"[^>]*>\\s*([^<]+)\\s*</a>`,
      "i"
    );
    const nameMatch = html.match(nameRegex);

    // Find price
    const priceRegex = new RegExp(
      `data-product-id="${id}"[\\s\\S]*?<span[^>]*class="[^"]*price[^"]*"[^>]*>([^<]+)</span>`,
      "i"
    );
    const priceMatch = html.match(priceRegex);

    // Find shop name
    const shopRegex = new RegExp(
      `data-product-id="${id}"[\\s\\S]*?<div[^>]*class="[^"]*shop-name[^"]*"[^>]*>([^<]+)</div>`,
      "i"
    );
    const shopMatch = html.match(shopRegex);

    if (imgMatch) {
      items.push({
        id,
        name: nameMatch ? nameMatch[1].trim() : `Booth商品 #${id}`,
        imageUrl: imgMatch[1],
        price: priceMatch ? priceMatch[1].trim() : "",
        creator: shopMatch ? shopMatch[1].trim() : "",
      });
    }
  }

  return items;
}

async function downloadImage(url, filepath) {
  const dir = path.dirname(filepath);
  if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true });

  try {
    const res = await fetch(url);
    if (res.status === 200) {
      fs.writeFileSync(filepath, res.body);
      return true;
    }
    console.log(`  WARN: status ${res.status} for ${url}`);
    return false;
  } catch (e) {
    console.log(`  ERROR: ${e.message} for ${url}`);
    return false;
  }
}

function sanitizeId(name) {
  // 日本語名からIDを生成
  return name
    .toLowerCase()
    .replace(/[^\w\u3040-\u309F\u30A0-\u30FF\u4E00-\u9FFF\-]/g, "-")
    .replace(/-+/g, "-")
    .replace(/^-|-$/g, "")
    .slice(0, 40) || "item";
}

async function fetchCategory(category, targetCount, outDir, type) {
  console.log(`\n=== ${category} (${targetCount}件) ===\n`);

  const allItems = [];
  const pages = Math.ceil(targetCount / ITEMS_PER_PAGE) + 1; // 余分に取得

  for (let page = 1; page <= pages && allItems.length < targetCount; page++) {
    console.log(`\nPage ${page}/${pages}...`);
    try {
      const html = await fetchBoothPage(category, page);
      const items = parseItems(html);
      console.log(`  ${items.length}件取得`);

      for (const item of items) {
        if (allItems.length >= targetCount) break;
        if (allItems.find((a) => a.id === item.id)) continue;
        allItems.push(item);
      }
    } catch (e) {
      console.log(`  ERROR: ページ取得失敗 - ${e.message}`);
    }
    await sleep(DELAY_MS);
  }

  console.log(`\n合計 ${allItems.length}件。サムネイルダウンロード開始...\n`);

  let downloaded = 0;
  for (const item of allItems) {
    const ext = item.imageUrl.match(/\.(jpg|jpeg|png|webp)/i)?.[1] || "jpg";
    const filename = `${item.id}.${ext}`;
    const filepath = path.join(outDir, filename);

    if (fs.existsSync(filepath)) {
      console.log(`  SKIP (存在): ${item.name} (${item.id})`);
      downloaded++;
      continue;
    }

    console.log(`  DL ${downloaded + 1}/${allItems.length}: ${item.name} (${item.id})`);
    const ok = await downloadImage(item.imageUrl, filepath);
    if (ok) downloaded++;
    await sleep(DELAY_MS);
  }

  console.log(`\n${type}完了: ${downloaded}/${allItems.length}枚ダウンロード\n`);

  return allItems;
}

async function main() {
  console.log("Booth人気アイテム取得スクリプト");
  console.log(`リクエスト間隔: ${DELAY_MS}ms\n`);

  // ディレクトリ作成
  [AVATAR_DIR, OUTFIT_DIR, DATA_DIR].forEach((d) => {
    if (!fs.existsSync(d)) fs.mkdirSync(d, { recursive: true });
  });

  // アバター取得
  const avatars = await fetchCategory("3Dキャラクター", AVATAR_COUNT, AVATAR_DIR, "アバター");

  // 衣装取得
  const outfits = await fetchCategory("3D衣装", OUTFIT_COUNT, OUTFIT_DIR, "衣装");

  // データ保存（JSONで中間出力）
  const result = {
    fetchedAt: new Date().toISOString(),
    avatars: avatars.map((a) => ({
      boothId: a.id,
      name: a.name,
      boothUrl: `https://booth.pm/ja/items/${a.id}`,
      thumbnailFile: `${a.id}.${a.imageUrl.match(/\.(jpg|jpeg|png|webp)/i)?.[1] || "jpg"}`,
      price: a.price,
      creator: a.creator,
    })),
    outfits: outfits.map((o) => ({
      boothId: o.id,
      name: o.name,
      boothUrl: `https://booth.pm/ja/items/${o.id}`,
      thumbnailFile: `${o.id}.${o.imageUrl.match(/\.(jpg|jpeg|png|webp)/i)?.[1] || "jpg"}`,
      price: o.price,
      creator: o.creator,
    })),
  };

  const jsonPath = path.join(DATA_DIR, "booth-items.json");
  fs.writeFileSync(jsonPath, JSON.stringify(result, null, 2), "utf-8");
  console.log(`\nデータ保存: ${jsonPath}`);
  console.log(`アバター: ${avatars.length}件`);
  console.log(`衣装: ${outfits.length}件`);
  console.log("\n完了！");
}

main().catch(console.error);
