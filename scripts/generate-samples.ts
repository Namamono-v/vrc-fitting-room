/**
 * サンプル透過PNG生成スクリプト
 * 素体(base)と衣装(outfit)のデモ画像を生成する
 */
import * as fs from "fs";
import * as path from "path";

const WIDTH = 512;
const HEIGHT = 682; // 3:4 aspect

// 簡易PNGエンコーダー（非圧縮PNG）
function createPNG(width: number, height: number, pixels: Uint8Array): Buffer {
  // PNG signature
  const signature = Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]);

  // IHDR chunk
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(width, 0);
  ihdr.writeUInt32BE(height, 4);
  ihdr[8] = 8; // bit depth
  ihdr[9] = 6; // color type (RGBA)
  ihdr[10] = 0; // compression
  ihdr[11] = 0; // filter
  ihdr[12] = 0; // interlace
  const ihdrChunk = makeChunk("IHDR", ihdr);

  // IDAT chunk - raw pixel data with zlib
  const rawData = Buffer.alloc(height * (1 + width * 4));
  for (let y = 0; y < height; y++) {
    rawData[y * (1 + width * 4)] = 0; // filter none
    for (let x = 0; x < width; x++) {
      const srcIdx = (y * width + x) * 4;
      const dstIdx = y * (1 + width * 4) + 1 + x * 4;
      rawData[dstIdx] = pixels[srcIdx];     // R
      rawData[dstIdx + 1] = pixels[srcIdx + 1]; // G
      rawData[dstIdx + 2] = pixels[srcIdx + 2]; // B
      rawData[dstIdx + 3] = pixels[srcIdx + 3]; // A
    }
  }

  // Use zlib deflate
  const zlib = require("zlib");
  const compressed = zlib.deflateSync(rawData);
  const idatChunk = makeChunk("IDAT", compressed);

  // IEND chunk
  const iendChunk = makeChunk("IEND", Buffer.alloc(0));

  return Buffer.concat([signature, ihdrChunk, idatChunk, iendChunk]);
}

function makeChunk(type: string, data: Buffer): Buffer {
  const length = Buffer.alloc(4);
  length.writeUInt32BE(data.length, 0);
  const typeBuffer = Buffer.from(type, "ascii");
  const crcData = Buffer.concat([typeBuffer, data]);
  const crc = Buffer.alloc(4);
  crc.writeUInt32BE(crc32(crcData), 0);
  return Buffer.concat([length, typeBuffer, data, crc]);
}

function crc32(buf: Buffer): number {
  let crc = 0xffffffff;
  for (let i = 0; i < buf.length; i++) {
    crc ^= buf[i];
    for (let j = 0; j < 8; j++) {
      crc = (crc >>> 1) ^ (crc & 1 ? 0xedb88320 : 0);
    }
  }
  return (crc ^ 0xffffffff) >>> 0;
}

// 素体画像生成（人型シルエット、透過背景）
function generateBase(pose: string): Uint8Array {
  const pixels = new Uint8Array(WIDTH * HEIGHT * 4);

  // ポーズによる横オフセット
  const offsetX = pose === "side" ? 30 : pose === "back" ? -10 : 0;

  const cx = WIDTH / 2 + offsetX;

  for (let y = 0; y < HEIGHT; y++) {
    for (let x = 0; x < WIDTH; x++) {
      const idx = (y * WIDTH + x) * 4;
      const ny = y / HEIGHT;
      const dx = x - cx;

      let inside = false;

      // 頭 (円)
      const headCy = HEIGHT * 0.12;
      const headR = 38;
      if (Math.sqrt(dx * dx + (y - headCy) * (y - headCy)) < headR) inside = true;

      // 体 (楕円)
      const bodyCy = HEIGHT * 0.35;
      const bodyW = 50;
      const bodyH = 120;
      if (Math.pow(dx / bodyW, 2) + Math.pow((y - bodyCy) / bodyH, 2) < 1) inside = true;

      // 左脚
      const legCx1 = cx - 18 + (pose === "side" ? 10 : 0);
      const legDx1 = x - legCx1;
      if (ny > 0.52 && ny < 0.92 && Math.abs(legDx1) < 16) inside = true;

      // 右脚
      const legCx2 = cx + 18 + (pose === "side" ? -10 : 0);
      const legDx2 = x - legCx2;
      if (ny > 0.52 && ny < 0.92 && Math.abs(legDx2) < 16) inside = true;

      // 左腕
      const armAngle1 = pose === "side" ? 0.3 : -0.15;
      const armCy1 = HEIGHT * 0.25;
      const armLen = 100;
      const armX1 = cx - 45 + (y - armCy1) * armAngle1;
      if (y > armCy1 && y < armCy1 + armLen && Math.abs(x - armX1) < 10) inside = true;

      // 右腕
      const armAngle2 = pose === "side" ? -0.1 : 0.15;
      const armCy2 = HEIGHT * 0.25;
      const armX2 = cx + 45 + (y - armCy2) * armAngle2;
      if (y > armCy2 && y < armCy2 + armLen && Math.abs(x - armX2) < 10) inside = true;

      if (inside) {
        // 肌色
        pixels[idx] = 255;
        pixels[idx + 1] = 220;
        pixels[idx + 2] = 200;
        pixels[idx + 3] = 255;
      }
      // 透過
    }
  }

  return pixels;
}

// 衣装画像生成（衣装部分のみ、透過背景）
function generateOutfit(pose: string, color: [number, number, number], style: string): Uint8Array {
  const pixels = new Uint8Array(WIDTH * HEIGHT * 4);

  const offsetX = pose === "side" ? 30 : pose === "back" ? -10 : 0;
  const cx = WIDTH / 2 + offsetX;

  for (let y = 0; y < HEIGHT; y++) {
    for (let x = 0; x < WIDTH; x++) {
      const idx = (y * WIDTH + x) * 4;
      const ny = y / HEIGHT;
      const dx = x - cx;

      let inside = false;
      let alpha = 255;

      if (style === "uniform") {
        // 上半身（制服ジャケット）
        const bodyW = 52;
        const bodyTop = HEIGHT * 0.18;
        const bodyBottom = HEIGHT * 0.48;
        if (y > bodyTop && y < bodyBottom && Math.abs(dx) < bodyW) inside = true;

        // スカート
        const skirtTop = HEIGHT * 0.46;
        const skirtBottom = HEIGHT * 0.62;
        const skirtW = 40 + (y - skirtTop) * 0.3;
        if (y > skirtTop && y < skirtBottom && Math.abs(dx) < skirtW) inside = true;
      } else if (style === "maid") {
        // メイド服 - エプロン+ワンピース
        const bodyTop = HEIGHT * 0.18;
        const bodyBottom = HEIGHT * 0.65;
        const bodyW = 48 + Math.max(0, (y - HEIGHT * 0.4)) * 0.25;
        if (y > bodyTop && y < bodyBottom && Math.abs(dx) < bodyW) inside = true;

        // フリル（裾）
        if (y > HEIGHT * 0.62 && y < HEIGHT * 0.68) {
          const frill = Math.sin(x * 0.15) * 5;
          if (Math.abs(dx) < bodyW + frill) {
            inside = true;
            alpha = 200;
          }
        }
      } else if (style === "hoodie") {
        // パーカー + パンツ
        const bodyTop = HEIGHT * 0.15;
        const bodyBottom = HEIGHT * 0.50;
        const bodyW = 55;
        if (y > bodyTop && y < bodyBottom && Math.abs(dx) < bodyW) inside = true;

        // フード
        const hoodCy = HEIGHT * 0.13;
        const hoodR = 42;
        if (Math.sqrt(dx * dx + (y - hoodCy) * (y - hoodCy)) < hoodR && y < hoodCy + 20) inside = true;

        // パンツ
        const legCx1 = cx - 18;
        const legCx2 = cx + 18;
        if (ny > 0.49 && ny < 0.85) {
          if (Math.abs(x - legCx1) < 18 || Math.abs(x - legCx2) < 18) inside = true;
        }
      }

      if (inside) {
        pixels[idx] = color[0];
        pixels[idx + 1] = color[1];
        pixels[idx + 2] = color[2];
        pixels[idx + 3] = alpha;
      }
    }
  }

  return pixels;
}

// メイン
const outDir = path.join(__dirname, "..", "public", "renders", "kipfel");
const poses = ["front", "side", "back"];

const outfits: { id: string; color: [number, number, number]; style: string }[] = [
  { id: "valkyrie", color: [30, 30, 60], style: "uniform" },
  { id: "danzai-sailor", color: [40, 40, 80], style: "uniform" },
  { id: "victorian-maid", color: [60, 20, 60], style: "maid" },
  { id: "hoodie-set", color: [240, 240, 245], style: "hoodie" },
  { id: "grimoire", color: [20, 10, 30], style: "maid" },
  { id: "hollow-swimsuit", color: [100, 180, 220], style: "uniform" },
  { id: "noble-nexus", color: [50, 50, 50], style: "hoodie" },
];

console.log("サンプル画像生成中...");

for (const pose of poses) {
  // 素体
  const basePixels = generateBase(pose);
  const basePng = createPNG(WIDTH, HEIGHT, basePixels);
  const basePath = path.join(outDir, "base", `${pose}.png`);
  fs.mkdirSync(path.dirname(basePath), { recursive: true });
  fs.writeFileSync(basePath, basePng);
  console.log(`  base/${pose}.png (${basePng.length} bytes)`);

  // 衣装
  for (const outfit of outfits) {
    const outfitPixels = generateOutfit(pose, outfit.color, outfit.style);
    const outfitPng = createPNG(WIDTH, HEIGHT, outfitPixels);
    const outfitPath = path.join(outDir, "outfit", outfit.id, `${pose}.png`);
    fs.mkdirSync(path.dirname(outfitPath), { recursive: true });
    fs.writeFileSync(outfitPath, outfitPng);
    console.log(`  outfit/${outfit.id}/${pose}.png (${outfitPng.length} bytes)`);
  }
}

console.log("\n完了！");
