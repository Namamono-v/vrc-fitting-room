export interface Pose {
  id: string;
  label: string;
}

export type OutfitGenre = "all" | "制服" | "メイド" | "カジュアル" | "ゴスロリ" | "ワンピース" | "水着" | "和服" | "ドレス" | "スポーティ" | "パンク" | "セクシー" | "コスプレ";

export interface Outfit {
  id: string;
  name: string;
  boothUrl: string;
  creator: string;
  price: string;
  genre: OutfitGenre;
  thumbnailUrl: string;
  contributor?: string; // データ提供者（匿名の場合は未設定 or "匿名"）
}

export interface Avatar {
  id: string;
  name: string;
  boothUrl: string;
  thumbnailUrl: string;
  outfits: Outfit[];
  supportedOutfitIds?: string[]; // 対応衣装ID。未設定=全対応
}

export interface Catalog {
  poses: Pose[];
  avatars: Avatar[];
  genres: { id: OutfitGenre; label: string }[];
}
