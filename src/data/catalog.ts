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
  {
    id: "urban-tech-obsidian",
    name: "Urban Tech Obsidian",
    boothUrl: "https://booth.pm/ja/items/4590436",
    creator: "Siromori",
    price: "",
    genre: "カジュアル",
    thumbnailUrl: "/thumbnails/outfits/4590436.jpg",
  },
  {
    id: "hoodie-set",
    name: "Hoodie set",
    boothUrl: "https://booth.pm/ja/items/7065708",
    creator: "Noem",
    price: "",
    genre: "カジュアル",
    thumbnailUrl: "/thumbnails/outfits/7065708.jpg",
  },
  {
    id: "dobuita-sukajan",
    name: "DOBUITA STYLE スカジャン",
    boothUrl: "https://booth.pm/ja/items/5203007",
    creator: "metasukavr",
    price: "",
    genre: "カジュアル",
    thumbnailUrl: "/thumbnails/outfits/5203007.jpg",
  },
  {
    id: "valkyrie",
    name: "第七特務機関ワルキューレ",
    boothUrl: "https://booth.pm/ja/items/6714930",
    creator: "EXTENSION CLOTHING",
    price: "",
    genre: "コスプレ",
    thumbnailUrl: "/thumbnails/outfits/6714930.jpg",
  },
  {
    id: "noir-luxe",
    name: "Noir Luxe",
    boothUrl: "https://booth.pm/ja/items/6447797",
    creator: "velvetsky",
    price: "",
    genre: "ドレス",
    thumbnailUrl: "/thumbnails/outfits/6447797.jpg",
  },
  {
    id: "honmei-knit",
    name: "本命ニット",
    boothUrl: "https://booth.pm/ja/items/6110958",
    creator: "てんぱすおおもり",
    price: "",
    genre: "カジュアル",
    thumbnailUrl: "/thumbnails/outfits/6110958.jpg",
  },
  {
    id: "ma1-hoodie-set",
    name: "BIG SILHOUETTE MA1 & HOODIE SET",
    boothUrl: "https://booth.pm/ja/items/5572679",
    creator: "MAISONDARC.",
    price: "",
    genre: "カジュアル",
    thumbnailUrl: "/thumbnails/outfits/5572679.jpg",
  },
  {
    id: "shadow-lily",
    name: "ShadowLily",
    boothUrl: "https://booth.pm/ja/items/6535704",
    creator: "KNIVES DESIGN",
    price: "",
    genre: "ゴスロリ",
    thumbnailUrl: "/thumbnails/outfits/6535704.jpg",
  },
  {
    id: "danzai-bunny",
    name: "断罪バニー",
    boothUrl: "https://booth.pm/ja/items/6633647",
    creator: "EXTENSION CLOTHING",
    price: "",
    genre: "セクシー",
    thumbnailUrl: "/thumbnails/outfits/6633647.jpg",
  },
  {
    id: "classical-chic",
    name: "Classical chic",
    boothUrl: "https://booth.pm/ja/items/5141327",
    creator: "Chocolate rice",
    price: "",
    genre: "ドレス",
    thumbnailUrl: "/thumbnails/outfits/5141327.jpg",
  },
  {
    id: "ever-after",
    name: "EverAfter",
    boothUrl: "https://booth.pm/ja/items/5615136",
    creator: "ALICE",
    price: "",
    genre: "ドレス",
    thumbnailUrl: "/thumbnails/outfits/5615136.jpg",
  },
  {
    id: "danzai-sailor",
    name: "断罪セーラー",
    boothUrl: "https://booth.pm/ja/items/5485641",
    creator: "EXTENSION CLOTHING",
    price: "",
    genre: "制服",
    thumbnailUrl: "/thumbnails/outfits/5485641.jpg",
  },
  {
    id: "midnight-school",
    name: "ミッドナイトスクール",
    boothUrl: "https://booth.pm/ja/items/5683588",
    creator: "OATH",
    price: "",
    genre: "制服",
    thumbnailUrl: "/thumbnails/outfits/5683588.jpg",
  },
  {
    id: "devidevi-parka",
    name: "でびでびぱーかー",
    boothUrl: "https://booth.pm/ja/items/6176948",
    creator: "AQUA LAILA",
    price: "",
    genre: "カジュアル",
    thumbnailUrl: "/thumbnails/outfits/6176948.jpg",
  },
  {
    id: "hyper-tech-evo",
    name: "HYPER TECH EVO IGNITE",
    boothUrl: "https://booth.pm/ja/items/5053741",
    creator: "EXTENSION CLOTHING",
    price: "",
    genre: "スポーティ",
    thumbnailUrl: "/thumbnails/outfits/5053741.jpg",
  },
  {
    id: "grimoire",
    name: "グリモワール",
    boothUrl: "https://booth.pm/ja/items/6194760",
    creator: "C.C.MATILDA",
    price: "",
    genre: "ゴスロリ",
    thumbnailUrl: "/thumbnails/outfits/6194760.jpg",
  },
  {
    id: "lop-ear-mine",
    name: "Lop Ear Mine",
    boothUrl: "https://booth.pm/ja/items/6721544",
    creator: "てんぱすおおもり",
    price: "",
    genre: "カジュアル",
    thumbnailUrl: "/thumbnails/outfits/6721544.jpg",
  },
  {
    id: "funky-street-vibe",
    name: "Funky Street Vibe",
    boothUrl: "https://booth.pm/ja/items/6521723",
    creator: "Chocolate rice",
    price: "",
    genre: "カジュアル",
    thumbnailUrl: "/thumbnails/outfits/6521723.jpg",
  },
  {
    id: "sailor-maiden",
    name: "Sailor Maiden",
    boothUrl: "https://booth.pm/ja/items/7542241",
    creator: "Ene Collection",
    price: "",
    genre: "制服",
    thumbnailUrl: "/thumbnails/outfits/7542241.jpg",
  },
  {
    id: "loose-punk",
    name: "Loose Punk",
    boothUrl: "https://booth.pm/ja/items/6033509",
    creator: "3BON",
    price: "",
    genre: "パンク",
    thumbnailUrl: "/thumbnails/outfits/6033509.jpg",
  },
  {
    id: "morbid-angel",
    name: "MorbidAngel",
    boothUrl: "https://booth.pm/ja/items/5879794",
    creator: "ALICE",
    price: "",
    genre: "ゴスロリ",
    thumbnailUrl: "/thumbnails/outfits/5879794.jpg",
  },
  {
    id: "night-in-yoshiwara",
    name: "ナイト・イン・ヨシワラ",
    boothUrl: "https://booth.pm/ja/items/5359699",
    creator: "VAGRANT",
    price: "",
    genre: "和服",
    thumbnailUrl: "/thumbnails/outfits/5359699.jpg",
  },
  {
    id: "arcane-attire",
    name: "Arcane Attire",
    boothUrl: "https://booth.pm/ja/items/6151859",
    creator: "Pirouette",
    price: "",
    genre: "ゴスロリ",
    thumbnailUrl: "/thumbnails/outfits/6151859.jpg",
  },
  {
    id: "meira",
    name: "Meira",
    boothUrl: "https://booth.pm/ja/items/6971758",
    creator: "cherry neru",
    price: "",
    genre: "ドレス",
    thumbnailUrl: "/thumbnails/outfits/6971758.jpg",
  },
  {
    id: "noble-nexus",
    name: "Noble Nexus",
    boothUrl: "https://booth.pm/ja/items/6650136",
    creator: "cherry neru",
    price: "",
    genre: "ドレス",
    thumbnailUrl: "/thumbnails/outfits/6650136.jpg",
  },
  {
    id: "midnight-fox",
    name: "Midnight Fox 2.0",
    boothUrl: "https://booth.pm/ja/items/5208776",
    creator: "CYCR (Cyber Critter)",
    price: "",
    genre: "コスプレ",
    thumbnailUrl: "/thumbnails/outfits/5208776.jpg",
  },
  {
    id: "winter-glow",
    name: "Winter Glow",
    boothUrl: "https://booth.pm/ja/items/6349460",
    creator: "cherry neru",
    price: "",
    genre: "カジュアル",
    thumbnailUrl: "/thumbnails/outfits/6349460.jpg",
  },
  {
    id: "crepscolo",
    name: "CREPSCOLO",
    boothUrl: "https://booth.pm/ja/items/6010473",
    creator: "mirukuru",
    price: "",
    genre: "ドレス",
    thumbnailUrl: "/thumbnails/outfits/6010473.jpg",
  },
  {
    id: "nightmare",
    name: "NIGHTMARE",
    boothUrl: "https://booth.pm/ja/items/5196178",
    creator: "EXTENSION CLOTHING",
    price: "",
    genre: "ゴスロリ",
    thumbnailUrl: "/thumbnails/outfits/5196178.jpg",
  },
  {
    id: "sol-paraiso",
    name: "Sol Paraiso",
    boothUrl: "https://booth.pm/ja/items/7599798",
    creator: "Virtual Casual",
    price: "",
    genre: "水着",
    thumbnailUrl: "/thumbnails/outfits/7599798.jpg",
  },
  {
    id: "victorian-whiskers-maid",
    name: "麗猫メイド Victorian Whiskers Maid",
    boothUrl: "https://booth.pm/ja/items/6397984",
    creator: "murasaki-ya",
    price: "",
    genre: "メイド",
    thumbnailUrl: "/thumbnails/outfits/6397984.jpg",
  },
  {
    id: "danzai-china",
    name: "断罪チャイナ",
    boothUrl: "https://booth.pm/ja/items/5760880",
    creator: "EXTENSION CLOTHING",
    price: "",
    genre: "和服",
    thumbnailUrl: "/thumbnails/outfits/5760880.jpg",
  },
  {
    id: "punk-furisode-parka",
    name: "パンク系振袖パーカーコーデセット",
    boothUrl: "https://booth.pm/ja/items/5298514",
    creator: "BLUESTELLA",
    price: "",
    genre: "パンク",
    thumbnailUrl: "/thumbnails/outfits/5298514.jpg",
  },
  {
    id: "koakuma-succubus",
    name: "小悪魔サキュバス リリス",
    boothUrl: "https://booth.pm/ja/items/6307927",
    creator: "EXTENSION CLOTHING",
    price: "",
    genre: "セクシー",
    thumbnailUrl: "/thumbnails/outfits/6307927.jpg",
  },
  {
    id: "gothic-doll",
    name: "Gothic Doll",
    boothUrl: "https://booth.pm/ja/items/5922237",
    creator: "SHOP HEILON",
    price: "",
    genre: "ゴスロリ",
    thumbnailUrl: "/thumbnails/outfits/5922237.jpg",
  },
  {
    id: "midnight-reverie",
    name: "Midnight Reverie",
    boothUrl: "https://booth.pm/ja/items/7002606",
    creator: "Ene Collection",
    price: "",
    genre: "ドレス",
    thumbnailUrl: "/thumbnails/outfits/7002606.jpg",
  },
  {
    id: "yoi",
    name: "宵",
    boothUrl: "https://booth.pm/ja/items/6959007",
    creator: "choco*shop",
    price: "",
    genre: "和服",
    thumbnailUrl: "/thumbnails/outfits/6959007.jpg",
  },
  {
    id: "danzai-sister",
    name: "断罪シスター",
    boothUrl: "https://booth.pm/ja/items/6175348",
    creator: "EXTENSION CLOTHING",
    price: "",
    genre: "コスプレ",
    thumbnailUrl: "/thumbnails/outfits/6175348.jpg",
  },
  {
    id: "evening-glow",
    name: "EveningGlow",
    boothUrl: "https://booth.pm/ja/items/6279994",
    creator: "ALICE",
    price: "",
    genre: "ドレス",
    thumbnailUrl: "/thumbnails/outfits/6279994.jpg",
  },
  {
    id: "urban-grace-city",
    name: "Urban Grace - City",
    boothUrl: "https://booth.pm/ja/items/6569464",
    creator: "VELLIE",
    price: "",
    genre: "カジュアル",
    thumbnailUrl: "/thumbnails/outfits/6569464.jpg",
  },
  {
    id: "velvet-grace",
    name: "ベルベット・グレイス",
    boothUrl: "https://booth.pm/ja/items/6094015",
    creator: "Delice Haute",
    price: "",
    genre: "ドレス",
    thumbnailUrl: "/thumbnails/outfits/6094015.jpg",
  },
  {
    id: "precious-school-uniforms",
    name: "Precious School Uniforms",
    boothUrl: "https://booth.pm/ja/items/6399824",
    creator: "SASA-Cafe",
    price: "",
    genre: "制服",
    thumbnailUrl: "/thumbnails/outfits/6399824.jpg",
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
    { id: "制服", label: "制服" },
    { id: "カジュアル", label: "カジュアル" },
    { id: "メイド", label: "メイド" },
    { id: "ゴスロリ", label: "ゴスロリ" },
    { id: "水着", label: "水着" },
    { id: "和服", label: "和服" },
    { id: "ドレス", label: "ドレス" },
    { id: "スポーティ", label: "スポーティ" },
    { id: "パンク", label: "パンク" },
    { id: "セクシー", label: "セクシー" },
    { id: "コスプレ", label: "コスプレ" },
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
    {
      id: "kipfel",
      name: "キプフェル",
      boothUrl: "https://booth.pm/ja/items/5813187",
      thumbnailUrl: "/thumbnails/avatars/5813187.jpg",
      outfits: sharedOutfits,
    },
    {
      id: "shinano",
      name: "しなの",
      boothUrl: "https://booth.pm/ja/items/6106863",
      thumbnailUrl: "/thumbnails/avatars/6106863.jpg",
      outfits: sharedOutfits,
    },
    {
      id: "manuka",
      name: "マヌカ",
      boothUrl: "https://booth.pm/ja/items/5058077",
      thumbnailUrl: "/thumbnails/avatars/5058077.jpg",
      outfits: sharedOutfits,
    },
    {
      id: "miltina",
      name: "ミルティナ",
      boothUrl: "https://booth.pm/ja/items/6538026",
      thumbnailUrl: "/thumbnails/avatars/6538026.jpg",
      outfits: sharedOutfits,
    },
    {
      id: "rurune",
      name: "ルルネ",
      boothUrl: "https://booth.pm/ja/items/5957830",
      thumbnailUrl: "/thumbnails/avatars/5957830.jpg",
      outfits: sharedOutfits,
    },
    {
      id: "mamehinata",
      name: "まめひなた",
      boothUrl: "https://booth.pm/ja/items/4340548",
      thumbnailUrl: "/thumbnails/avatars/4340548.jpg",
      outfits: sharedOutfits,
    },
    {
      id: "kikyo",
      name: "桔梗",
      boothUrl: "https://booth.pm/ja/items/3681787",
      thumbnailUrl: "/thumbnails/avatars/3681787.jpg",
      outfits: sharedOutfits,
    },
    {
      id: "celestia",
      name: "セレスティア",
      boothUrl: "https://booth.pm/ja/items/4035411",
      thumbnailUrl: "/thumbnails/avatars/4035411.jpg",
      outfits: sharedOutfits,
    },
    {
      id: "chocolat",
      name: "ショコラ",
      boothUrl: "https://booth.pm/ja/items/6405390",
      thumbnailUrl: "/thumbnails/avatars/6405390.jpg",
      outfits: sharedOutfits,
    },
    {
      id: "sio",
      name: "しお",
      boothUrl: "https://booth.pm/ja/items/5650156",
      thumbnailUrl: "/thumbnails/avatars/5650156.jpg",
      outfits: sharedOutfits,
    },
    {
      id: "zome",
      name: "ゾメちゃん",
      boothUrl: "https://booth.pm/ja/items/4118550",
      thumbnailUrl: "/thumbnails/avatars/4118550.jpg",
      outfits: sharedOutfits,
    },
    {
      id: "mafuyu",
      name: "真冬",
      boothUrl: "https://booth.pm/ja/items/5007531",
      thumbnailUrl: "/thumbnails/avatars/5007531.jpg",
      outfits: sharedOutfits,
    },
    {
      id: "karin",
      name: "カリン",
      boothUrl: "https://booth.pm/ja/items/3470989",
      thumbnailUrl: "/thumbnails/avatars/3470989.jpg",
      outfits: sharedOutfits,
    },
    {
      id: "raddoll-v3",
      name: "RadDoll V3",
      boothUrl: "https://booth.pm/ja/items/3741802",
      thumbnailUrl: "/thumbnails/avatars/3741802.jpg",
      outfits: sharedOutfits,
    },
    {
      id: "komano",
      name: "狛乃",
      boothUrl: "https://booth.pm/ja/items/5260363",
      thumbnailUrl: "/thumbnails/avatars/5260363.jpg",
      outfits: sharedOutfits,
    },
    {
      id: "milfy",
      name: "ミルフィ",
      boothUrl: "https://booth.pm/ja/items/6571299",
      thumbnailUrl: "/thumbnails/avatars/6571299.jpg",
      outfits: sharedOutfits,
    },
    {
      id: "maiya",
      name: "舞夜",
      boothUrl: "https://booth.pm/ja/items/3390957",
      thumbnailUrl: "/thumbnails/avatars/3390957.jpg",
      outfits: sharedOutfits,
    },
    {
      id: "rusk",
      name: "ラスク",
      boothUrl: "https://booth.pm/ja/items/2559783",
      thumbnailUrl: "/thumbnails/avatars/2559783.jpg",
      outfits: sharedOutfits,
    },
    {
      id: "ferris",
      name: "フェリス",
      boothUrl: "https://booth.pm/ja/items/5385176",
      thumbnailUrl: "/thumbnails/avatars/5385176.jpg",
      outfits: sharedOutfits,
    },
    {
      id: "moe",
      name: "萌",
      boothUrl: "https://booth.pm/ja/items/4667400",
      thumbnailUrl: "/thumbnails/avatars/4667400.jpg",
      outfits: sharedOutfits,
    },
    {
      id: "mizuki",
      name: "瑞希",
      boothUrl: "https://booth.pm/ja/items/5132797",
      thumbnailUrl: "/thumbnails/avatars/5132797.jpg",
      outfits: sharedOutfits,
    },
    {
      id: "eku",
      name: "エク",
      boothUrl: "https://booth.pm/ja/items/7328764",
      thumbnailUrl: "/thumbnails/avatars/7328764.jpg",
      outfits: sharedOutfits,
    },
    {
      id: "lasyusha",
      name: "ラシューシャ",
      boothUrl: "https://booth.pm/ja/items/4825073",
      thumbnailUrl: "/thumbnails/avatars/4825073.jpg",
      outfits: sharedOutfits,
    },
    {
      id: "chiffon",
      name: "シフォン",
      boothUrl: "https://booth.pm/ja/items/5354471",
      thumbnailUrl: "/thumbnails/avatars/5354471.jpg",
      outfits: sharedOutfits,
    },
    {
      id: "marycia",
      name: "マリシア",
      boothUrl: "https://booth.pm/ja/items/6305948",
      thumbnailUrl: "/thumbnails/avatars/6305948.jpg",
      outfits: sharedOutfits,
    },
    {
      id: "yuko-oneesama",
      name: "幽狐族のお姉様",
      boothUrl: "https://booth.pm/ja/items/1484117",
      thumbnailUrl: "/thumbnails/avatars/1484117.jpg",
      outfits: sharedOutfits,
    },
    {
      id: "mao",
      name: "真央",
      boothUrl: "https://booth.pm/ja/items/6846646",
      thumbnailUrl: "/thumbnails/avatars/6846646.jpg",
      outfits: sharedOutfits,
    },
    {
      id: "bokusei",
      name: "墨惺",
      boothUrl: "https://booth.pm/ja/items/5727810",
      thumbnailUrl: "/thumbnails/avatars/5727810.jpg",
      outfits: sharedOutfits,
    },
    {
      id: "lumina",
      name: "ルミナ",
      boothUrl: "https://booth.pm/ja/items/7502898",
      thumbnailUrl: "/thumbnails/avatars/7502898.jpg",
      outfits: sharedOutfits,
    },
    {
      id: "ichigo",
      name: "イチゴ",
      boothUrl: "https://booth.pm/ja/items/7328789",
      thumbnailUrl: "/thumbnails/avatars/7328789.jpg",
      outfits: sharedOutfits,
    },
    {
      id: "rindo",
      name: "竜胆",
      boothUrl: "https://booth.pm/ja/items/3443188",
      thumbnailUrl: "/thumbnails/avatars/3443188.jpg",
      outfits: sharedOutfits,
    },
    {
      id: "ramune",
      name: "ラムネ",
      boothUrl: "https://booth.pm/ja/items/7699667",
      thumbnailUrl: "/thumbnails/avatars/7699667.jpg",
      outfits: sharedOutfits,
    },
    {
      id: "rinasciita",
      name: "リナシータ",
      boothUrl: "https://booth.pm/ja/items/7475899",
      thumbnailUrl: "/thumbnails/avatars/7475899.jpg",
      outfits: sharedOutfits,
    },
    {
      id: "ash",
      name: "アッシュ",
      boothUrl: "https://booth.pm/ja/items/3234473",
      thumbnailUrl: "/thumbnails/avatars/3234473.jpg",
      outfits: sharedOutfits,
    },
    {
      id: "airi",
      name: "愛莉",
      boothUrl: "https://booth.pm/ja/items/6082686",
      thumbnailUrl: "/thumbnails/avatars/6082686.jpg",
      outfits: sharedOutfits,
    },
    {
      id: "haolan",
      name: "ハオラン",
      boothUrl: "https://booth.pm/ja/items/3818504",
      thumbnailUrl: "/thumbnails/avatars/3818504.jpg",
      outfits: sharedOutfits,
    },
    {
      id: "lime",
      name: "ライム",
      boothUrl: "https://booth.pm/ja/items/4876459",
      thumbnailUrl: "/thumbnails/avatars/4876459.jpg",
      outfits: sharedOutfits,
    },
    {
      id: "usasaki",
      name: "うささき",
      boothUrl: "https://booth.pm/ja/items/3550881",
      thumbnailUrl: "/thumbnails/avatars/3550881.jpg",
      outfits: sharedOutfits,
    },
    {
      id: "nanase-noir",
      name: "ナナセ・ノワール",
      boothUrl: "https://booth.pm/ja/items/5827815",
      thumbnailUrl: "/thumbnails/avatars/5827815.jpg",
      outfits: sharedOutfits,
    },
    {
      id: "platinum",
      name: "プラチナ",
      boothUrl: "https://booth.pm/ja/items/3950859",
      thumbnailUrl: "/thumbnails/avatars/3950859.jpg",
      outfits: sharedOutfits,
    },
    {
      id: "kanata-konata",
      name: "彼方＆此方",
      boothUrl: "https://booth.pm/ja/items/6813995",
      thumbnailUrl: "/thumbnails/avatars/6813995.jpg",
      outfits: sharedOutfits,
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
