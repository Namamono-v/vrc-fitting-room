import { createClient } from "@supabase/supabase-js";
import { Avatar, Catalog, Outfit, OutfitGenre } from "@/types";

const supabaseUrl =
  process.env.NEXT_PUBLIC_SUPABASE_URL ?? "https://ksotijmuhednzfiahhoo.supabase.co";
const supabaseAnonKey =
  process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY ?? "sb_publishable_IPqD4wfiJzK1PBOZDhLKVA_iqzcfv6y";

export const supabase = createClient(supabaseUrl, supabaseAnonKey);

const STORAGE_BUCKET = "fitting-room";

// ── Supabase Storage の公開URL ──────────────────────────────
/** storage バケット内パスから公開URLを返す */
export function getImageUrl(path: string): string {
  return `${supabaseUrl}/storage/v1/object/public/${STORAGE_BUCKET}/${path}`;
}

/** アバターID + ファイル名 → レンダー画像URL (Supabase Storage) */
export function getSupabaseRenderUrl(avatarId: string, filename: string): string {
  return getImageUrl(`renders/${avatarId}/${filename}.png`);
}

// ── DB 行の型 ───────────────────────────────────────────────
interface FittingAvatarRow {
  id: string;
  name: string;
  booth_url: string;
  thumbnail_url: string;
  supported_outfit_ids?: string[] | null;
}

interface FittingOutfitRow {
  avatar_id: string;
  outfit_id: string;
  name: string;
  booth_url: string;
  creator?: string | null;
  price?: string | null;
  genre?: string | null;
  thumbnail_url?: string | null;
  contributors?: string[] | null;
  hidden?: boolean | null;
}

// ── fetchCatalogFromSupabase ────────────────────────────────
/**
 * fitting_avatars + fitting_outfits を取得し Catalog 型で返す。
 * poses / genres は DB に持たないので null を返し、呼び出し側で静的データを使う。
 */
export async function fetchCatalogFromSupabase(): Promise<{
  avatars: Avatar[];
} | null> {
  try {
    const [avatarsRes, outfitsRes] = await Promise.all([
      supabase.from("fitting_avatars").select("*"),
      supabase.from("fitting_outfits").select("*"),
    ]);

    if (avatarsRes.error) {
      console.warn("[Supabase] fitting_avatars fetch error:", avatarsRes.error.message);
      return null;
    }
    if (outfitsRes.error) {
      console.warn("[Supabase] fitting_outfits fetch error:", outfitsRes.error.message);
      return null;
    }

    const avatarRows = (avatarsRes.data ?? []) as FittingAvatarRow[];
    const outfitRows = (outfitsRes.data ?? []) as FittingOutfitRow[];

    // outfitRows を avatar_id でグルーピング
    const outfitsByAvatar = new Map<string, Outfit[]>();
    for (const row of outfitRows) {
      const outfit: Outfit = {
        id: row.outfit_id,
        name: row.name,
        boothUrl: row.booth_url,
        creator: row.creator ?? "",
        price: row.price ?? "",
        genre: (row.genre as OutfitGenre) ?? "カジュアル",
        thumbnailUrl: row.thumbnail_url ?? `/thumbnails/outfits/${row.outfit_id}.jpg`,
        contributors: row.contributors ?? undefined,
        hidden: row.hidden ?? undefined,
      };
      const list = outfitsByAvatar.get(row.avatar_id) ?? [];
      list.push(outfit);
      outfitsByAvatar.set(row.avatar_id, list);
    }

    const avatars: Avatar[] = avatarRows.map((row) => ({
      id: row.id,
      name: row.name,
      boothUrl: row.booth_url,
      thumbnailUrl: row.thumbnail_url,
      outfits: outfitsByAvatar.get(row.id) ?? [],
      supportedOutfitIds: row.supported_outfit_ids ?? undefined,
    }));

    return { avatars };
  } catch (err) {
    console.warn("[Supabase] fetch failed:", err);
    return null;
  }
}
