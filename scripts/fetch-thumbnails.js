#!/usr/bin/env node
/**
 * fetch-thumbnails.js
 *
 * Reads catalog.ts, extracts all boothUrl values for avatars and outfits,
 * fetches each BOOTH product page to get the OGP image (og:image),
 * and downloads the image to public/thumbnails/{avatars|outfits}/{boothItemId}.jpg
 *
 * Usage: node scripts/fetch-thumbnails.js
 */

const https = require("https");
const http = require("http");
const fs = require("fs");
const path = require("path");
const { URL } = require("url");

// ---------------------------------------------------------------------------
// Config
// ---------------------------------------------------------------------------
const PROJECT_ROOT = path.resolve(__dirname, "..");
const CATALOG_PATH = path.join(PROJECT_ROOT, "src", "data", "catalog.ts");
const THUMBNAILS_DIR = path.join(PROJECT_ROOT, "public", "thumbnails");
const DELAY_MS = 500;

// Allowed domains for fetching
const ALLOWED_FETCH_DOMAINS = [
  "booth.pm",
  "booth.pximg.net",
];

function isAllowedUrl(urlStr) {
  try {
    const u = new URL(urlStr);
    return ALLOWED_FETCH_DOMAINS.some(
      (d) => u.hostname === d || u.hostname.endsWith("." + d)
    );
  } catch {
    return false;
  }
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/** Extract the numeric BOOTH item ID from a booth URL */
function extractBoothId(boothUrl) {
  const m = boothUrl.match(/items\/(\d+)/);
  return m ? m[1] : null;
}

/** Simple sleep */
function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

/**
 * Fetch a URL, following up to `maxRedirects` redirects.
 * Returns { statusCode, headers, body } where body is a Buffer.
 */
function fetch(url, maxRedirects = 5) {
  return new Promise((resolve, reject) => {
    if (!isAllowedUrl(url)) {
      return reject(new Error(`Blocked: ${url} is not in the allowed domain list`));
    }
    if (maxRedirects < 0) {
      return reject(new Error("Too many redirects"));
    }
    const parsed = new URL(url);
    const mod = parsed.protocol === "https:" ? https : http;
    const req = mod.get(
      url,
      {
        headers: {
          "User-Agent":
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
          Accept:
            "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8",
          "Accept-Language": "ja,en;q=0.5",
        },
      },
      (res) => {
        if (
          res.statusCode >= 300 &&
          res.statusCode < 400 &&
          res.headers.location
        ) {
          // Follow redirect
          let redirectUrl = res.headers.location;
          if (redirectUrl.startsWith("/")) {
            redirectUrl = `${parsed.protocol}//${parsed.host}${redirectUrl}`;
          }
          res.resume(); // drain the response
          return resolve(fetch(redirectUrl, maxRedirects - 1));
        }
        const chunks = [];
        res.on("data", (chunk) => chunks.push(chunk));
        res.on("end", () => {
          resolve({
            statusCode: res.statusCode,
            headers: res.headers,
            body: Buffer.concat(chunks),
          });
        });
        res.on("error", reject);
      }
    );
    req.on("error", reject);
  });
}

/**
 * Parse the catalog.ts file and extract avatar and outfit entries with boothUrls.
 * Returns { avatars: [{boothUrl, boothId}], outfits: [{boothUrl, boothId}] }
 */
function parseCatalog(filePath) {
  const content = fs.readFileSync(filePath, "utf-8");

  const avatars = [];
  const outfits = [];
  const seenOutfitIds = new Set();

  // --- Outfits: extract from sharedOutfits array ---
  const sharedStart = content.indexOf("const sharedOutfits");
  const baseCatalogStart = content.indexOf("const baseCatalog");

  if (sharedStart !== -1 && baseCatalogStart !== -1) {
    const outfitsBlock = content.substring(sharedStart, baseCatalogStart);
    const boothUrlRegex = /boothUrl:\s*"([^"]+)"/g;
    let m;
    while ((m = boothUrlRegex.exec(outfitsBlock)) !== null) {
      const boothUrl = m[1];
      const boothId = extractBoothId(boothUrl);
      if (boothId && !seenOutfitIds.has(boothId)) {
        seenOutfitIds.add(boothId);
        outfits.push({ boothUrl, boothId });
      }
    }
  }

  // --- Avatars: extract from baseCatalog's avatars array ---
  if (baseCatalogStart !== -1) {
    const avatarsBlock = content.substring(baseCatalogStart);
    // Each avatar has id + boothUrl on nearby lines; outfits are referenced
    // as `sharedOutfits` so there are no inline outfit boothUrls here.
    const boothUrlRegex = /boothUrl:\s*"([^"]+)"/g;
    let m;
    while ((m = boothUrlRegex.exec(avatarsBlock)) !== null) {
      const boothUrl = m[1];
      const boothId = extractBoothId(boothUrl);
      if (boothId) {
        avatars.push({ boothUrl, boothId });
      }
    }
  }

  return { avatars, outfits };
}

/**
 * Extract og:image URL from HTML content.
 */
function extractOgImage(html) {
  // Try various og:image patterns
  const patterns = [
    /<meta\s+property="og:image"\s+content="([^"]+)"/i,
    /<meta\s+content="([^"]+)"\s+property="og:image"/i,
    /<meta\s+property='og:image'\s+content='([^']+)'/i,
    /<meta\s+content='([^']+)'\s+property='og:image'/i,
  ];
  for (const pattern of patterns) {
    const m = html.match(pattern);
    if (m) return m[1];
  }
  return null;
}

/**
 * Download an image from a URL and save it to destPath.
 */
async function downloadImage(imageUrl, destPath) {
  const res = await fetch(imageUrl);
  if (res.statusCode !== 200) {
    throw new Error(`Image download failed with status ${res.statusCode}`);
  }
  fs.writeFileSync(destPath, res.body);
}

// ---------------------------------------------------------------------------
// Main
// ---------------------------------------------------------------------------

async function main() {
  console.log("Reading catalog.ts...");
  const { avatars, outfits } = parseCatalog(CATALOG_PATH);
  console.log(
    `Found ${avatars.length} avatars and ${outfits.length} outfits.\n`
  );

  // Ensure directories exist
  const avatarDir = path.join(THUMBNAILS_DIR, "avatars");
  const outfitDir = path.join(THUMBNAILS_DIR, "outfits");
  fs.mkdirSync(avatarDir, { recursive: true });
  fs.mkdirSync(outfitDir, { recursive: true });

  // Combine all items with their type
  const items = [
    ...avatars.map((a) => ({ ...a, type: "avatars" })),
    ...outfits.map((o) => ({ ...o, type: "outfits" })),
  ];

  let downloaded = 0;
  let skipped = 0;
  let failed = 0;

  for (const item of items) {
    const destDir =
      item.type === "avatars" ? avatarDir : outfitDir;
    const destPath = path.join(destDir, `${item.boothId}.jpg`);

    // Skip if already exists
    if (fs.existsSync(destPath)) {
      console.log(`[SKIP] ${item.type}/${item.boothId}.jpg already exists`);
      skipped++;
      continue;
    }

    try {
      console.log(
        `[FETCH] ${item.boothUrl} -> ${item.type}/${item.boothId}.jpg`
      );

      // Fetch the BOOTH product page
      const pageRes = await fetch(item.boothUrl);
      if (pageRes.statusCode !== 200) {
        console.log(
          `  [ERROR] Page returned status ${pageRes.statusCode}, skipping.`
        );
        failed++;
        await sleep(DELAY_MS);
        continue;
      }

      const html = pageRes.body.toString("utf-8");
      const ogImageUrl = extractOgImage(html);

      if (!ogImageUrl) {
        console.log("  [ERROR] No og:image found on page, skipping.");
        failed++;
        await sleep(DELAY_MS);
        continue;
      }

      // Download the OGP image
      console.log(`  [DOWNLOAD] ${ogImageUrl}`);
      await downloadImage(ogImageUrl, destPath);
      console.log(`  [OK] Saved to ${item.type}/${item.boothId}.jpg`);
      downloaded++;
    } catch (err) {
      console.log(`  [ERROR] ${err.message}`);
      failed++;
    }

    await sleep(DELAY_MS);
  }

  console.log("\n--- Summary ---");
  console.log(`Downloaded: ${downloaded}`);
  console.log(`Skipped (already exist): ${skipped}`);
  console.log(`Failed: ${failed}`);
  console.log(`Total: ${items.length}`);
}

main().catch((err) => {
  console.error("Fatal error:", err);
  process.exit(1);
});
