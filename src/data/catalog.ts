import { Avatar, Catalog, Outfit, OutfitGenre } from "@/types";

// ビルド時に public/data/catalog.json を読み込む（存在しない場合は空データ）
interface CatalogJson {
  avatars?: Array<{
    id: string;
    name: string;
    boothUrl: string;
    thumbnailUrl: string;
    outfits?: Array<{
      id: string;
      name: string;
      boothUrl: string;
      genre?: OutfitGenre;
      thumbnailUrl: string;
      creator?: string;
      price?: string;
    }>;
    supportedOutfitIds?: string[];
  }>;
}

let catalogJson: CatalogJson = { avatars: [] };
try {
  catalogJson = require("../../public/data/catalog.json") as CatalogJson;
} catch {
  // catalog.json が存在しない場合はハードコードデータのみ使用
}

/**
 * ハードコードデータと catalog.json をマージ
 * - 既存アバターの場合: JSON の outfits から新規分を追加、supportedOutfitIds を更新
 * - 新規アバターの場合: JSON のデータをそのまま追加
 */
function mergeCatalog(base: Catalog, json: CatalogJson): Catalog {
  if (!json.avatars || json.avatars.length === 0) return base;

  const baseAvatarMap = new Map(base.avatars.map((a) => [a.id, a]));
  const mergedAvatars = [...base.avatars];

  for (const jsonAvatar of json.avatars) {
    const existing = baseAvatarMap.get(jsonAvatar.id);
    if (existing) {
      // 既存アバター: JSON の outfits で新規分を追加
      if (jsonAvatar.outfits) {
        const existingIds = new Set(existing.outfits.map((o) => o.id));
        for (const jsonOutfit of jsonAvatar.outfits) {
          if (!existingIds.has(jsonOutfit.id)) {
            existing.outfits.push({
              id: jsonOutfit.id,
              name: jsonOutfit.name,
              boothUrl: jsonOutfit.boothUrl,
              creator: jsonOutfit.creator ?? "",
              price: jsonOutfit.price ?? "",
              genre: jsonOutfit.genre ?? ("カジュアル" as OutfitGenre),
              thumbnailUrl: jsonOutfit.thumbnailUrl,
            });
          }
        }
      }
      // supportedOutfitIds を JSON から更新（JSON にあれば上書き）
      if (jsonAvatar.supportedOutfitIds) {
        existing.supportedOutfitIds = jsonAvatar.supportedOutfitIds;
      }
    } else {
      // 新規アバター: JSON データから Avatar を生成
      const newAvatar: Avatar = {
        id: jsonAvatar.id,
        name: jsonAvatar.name,
        boothUrl: jsonAvatar.boothUrl,
        thumbnailUrl: jsonAvatar.thumbnailUrl,
        outfits: (jsonAvatar.outfits ?? []).map((o) => ({
          id: o.id,
          name: o.name,
          boothUrl: o.boothUrl,
          creator: o.creator ?? "",
          price: o.price ?? "",
          genre: o.genre ?? ("カジュアル" as OutfitGenre),
          thumbnailUrl: o.thumbnailUrl,
        })),
        supportedOutfitIds: jsonAvatar.supportedOutfitIds,
      };
      mergedAvatars.push(newAvatar);
    }
  }

  return { ...base, avatars: mergedAvatars };
}

const sharedOutfits: Outfit[] = [
  {
    id: "和服",
    name: "甚平セット",
    boothUrl: "https://antnestin.booth.pm/items/3333146",
    creator: "antnestin",
    price: "¥1,000",
    genre: "和服",
    thumbnailUrl: "/thumbnails/outfits/3333146.jpg",
  },
];

const baseCatalog: Catalog = {
  poses: [
    { id: "arms-crossed", label: "腕組み" },
    { id: "peace", label: "ピース" },
    { id: "shy", label: "照れ" },
    { id: "hip-hand", label: "腰に手" },
    { id: "cool-crossed", label: "クール腕組み" },
    { id: "relax", label: "リラックス" },
    { id: "point", label: "指差し" },
    { id: "seiza", label: "正座" },
    { id: "finger-up", label: "指立て" },
    { id: "guts", label: "ガッツ" },
  ],
  genres: [
    { id: "all" as OutfitGenre, label: "すべて" },
    { id: "和服", label: "和服" },
  ],
  avatars: [
    {
      id: "siniri_body_ver2.00",
      name: "新入り",
      boothUrl: "https://pkj-shop.booth.pm/items/4509553",
      thumbnailUrl: "/thumbnails/avatars/4509553.jpg",
      outfits: sharedOutfits,
      supportedOutfitIds: ["和服"],
    },
  ],
};

// ハードコードデータと catalog.json をマージして最終カタログを生成
export const catalog: Catalog = mergeCatalog(baseCatalog, catalogJson);

export function getBasePath(avatarId: string): string {
  return `/renders/${avatarId}/素体.png`;
}

/** boothUrl から BOOTH商品IDを抽出 */
function extractBoothId(boothUrl: string): string | null {
  const match = boothUrl.match(/items\/(\d+)/);
  return match ? match[1] : null;
}

/** outfitId → BOOTH ID の逆引きマップ（初回アクセス時に構築） */
let _outfitBoothIdMap: Record<string, string> | null = null;
function getOutfitBoothIdMap(): Record<string, string> {
  if (!_outfitBoothIdMap) {
    _outfitBoothIdMap = {};
    for (const avatar of catalog.avatars) {
      for (const outfit of avatar.outfits) {
        if (outfit.boothUrl && !_outfitBoothIdMap[outfit.id]) {
          const bid = extractBoothId(outfit.boothUrl);
          if (bid) _outfitBoothIdMap[outfit.id] = bid;
        }
      }
    }
  }
  return _outfitBoothIdMap;
}

export function getOutfitPath(avatarId: string, outfitId: string): string {
  const boothId = getOutfitBoothIdMap()[outfitId];
  const fileBase = boothId ?? outfitId;
  return `/renders/${avatarId}/${fileBase}.png`;
}

export function getOutfitPosePath(avatarId: string, outfitId: string, poseId: string): string {
  const boothId = getOutfitBoothIdMap()[outfitId];
  const fileBase = boothId ?? outfitId;
  return `/renders/${avatarId}/${fileBase}_${poseId}.png`;
}
