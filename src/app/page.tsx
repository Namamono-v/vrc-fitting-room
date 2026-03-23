"use client";

import { useState, useEffect } from "react";
import { catalog as staticCatalog } from "@/data/catalog";
import { fetchCatalogFromSupabase } from "@/lib/supabase";
import { useFittingRoom } from "@/hooks/useFittingRoom";
import { PreviewArea } from "@/components/PreviewArea";
import { PoseSelector } from "@/components/PoseSelector";
import { OutfitList } from "@/components/OutfitList";
import { BoothButton } from "@/components/BoothButton";
import { Avatar, Catalog, Outfit } from "@/types";

/**
 * 静的カタログと Supabase データをマージする。
 * - 同じ ID のアバターは outfits をマージ（Supabase 側の outfit を優先追加）
 * - 新規アバターはそのまま追加
 * - poses / genres は静的データをそのまま使う
 */
function mergeCatalogs(base: Catalog, supabaseAvatars: Avatar[]): Catalog {
  const merged = { ...base, avatars: [...base.avatars] };
  const avatarMap = new Map(merged.avatars.map((a) => [a.id, a]));

  for (const sbAvatar of supabaseAvatars) {
    const existing = avatarMap.get(sbAvatar.id);
    if (existing) {
      // 既存アバター: Supabase の outfits でマージ
      for (const outfit of sbAvatar.outfits) {
        // IDまたはboothUrl（BOOTH商品ID）で同一衣装を検索
        const idx = existing.outfits.findIndex(
          (o) => o.id === outfit.id || (o.boothUrl && outfit.boothUrl && o.boothUrl === outfit.boothUrl)
        );
        if (idx >= 0) {
          // 既存衣装: Supabaseデータで上書き（contributors等を反映）
          existing.outfits[idx] = { ...existing.outfits[idx], ...outfit, id: existing.outfits[idx].id };
        } else {
          existing.outfits.push(outfit);
        }
      }
      // Supabaseから来た衣装をsupportedOutfitIdsに追加
      if (existing.supportedOutfitIds) {
        for (const outfit of sbAvatar.outfits) {
          const matchedOutfit = existing.outfits.find(
            (o) => o.boothUrl && outfit.boothUrl && o.boothUrl === outfit.boothUrl
          );
          const idToAdd = matchedOutfit?.id ?? outfit.id;
          if (!existing.supportedOutfitIds.includes(idToAdd)) {
            existing.supportedOutfitIds.push(idToAdd);
          }
        }
      }
      // supportedOutfitIds: Supabase にあれば上書き
      if (sbAvatar.supportedOutfitIds) {
        existing.supportedOutfitIds = sbAvatar.supportedOutfitIds;
      }
    } else {
      // 新規アバター
      merged.avatars.push(sbAvatar);
      avatarMap.set(sbAvatar.id, sbAvatar);
    }
  }

  return merged;
}

export default function Home() {
  const [catalog, setCatalog] = useState<Catalog>(staticCatalog);
  const [isLoading, setIsLoading] = useState(true);

  // Client-side: Supabase からデータを取得してマージ
  useEffect(() => {
    let cancelled = false;
    fetchCatalogFromSupabase()
      .then((result) => {
        if (cancelled || !result) return;
        setCatalog((prev) => mergeCatalogs(prev, result.avatars));
      })
      .finally(() => {
        if (!cancelled) setIsLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const room = useFittingRoom(catalog);

  return (
    <div className="flex flex-col min-h-screen">
      {/* ヘッダー */}
      <header className="text-center py-4 flex-shrink-0">
        <h1 className="text-xl font-bold tracking-tight text-pink-500">
          🎀 アバター試着データベース
        </h1>
        <p className="text-[11px] text-gray-400 mt-0.5">
          アバターを選んで、衣装をクリックしてプレビュー
        </p>
        {isLoading && (
          <div className="flex items-center justify-center gap-1.5 mt-1.5">
            <div className="w-1.5 h-1.5 bg-pink-300 rounded-full animate-bounce [animation-delay:-0.3s]" />
            <div className="w-1.5 h-1.5 bg-pink-300 rounded-full animate-bounce [animation-delay:-0.15s]" />
            <div className="w-1.5 h-1.5 bg-pink-300 rounded-full animate-bounce" />
            <span className="text-[10px] text-pink-300 ml-1">データ読み込み中</span>
          </div>
        )}
      </header>

      {/* メイン: 3カラム */}
      <main className="flex-1 flex flex-col md:flex-row gap-3 px-3 pb-3 min-h-0 max-w-[1400px] mx-auto w-full">
        {/* 左: アバター一覧（縦スクロール） */}
        <div className="md:w-24 lg:w-28 flex-shrink-0 md:h-[calc(100vh-7rem)] overflow-hidden">
          <div className="flex md:flex-col gap-2 overflow-x-auto md:overflow-y-auto md:overflow-x-hidden h-full pr-0 md:pr-1 pb-2 md:pb-0">
            <h2 className="hidden md:block text-[10px] font-bold text-gray-400 uppercase tracking-wider mb-1 sticky top-0 bg-pink-50/90 backdrop-blur-sm py-1 z-10">
              アバター
            </h2>
            {catalog.avatars.map((avatar) => (
              <AvatarThumb
                key={avatar.id}
                name={avatar.name}
                thumbnailUrl={avatar.thumbnailUrl}
                isActive={room.avatarId === avatar.id}
                onSelect={() => room.setAvatarId(avatar.id)}
              />
            ))}
          </div>
        </div>

        {/* 中央: プレビュー + ポーズ */}
        <div className="flex-1 flex flex-col min-w-0">
          <PreviewArea
            src={room.previewSrc}
            avatarName={room.avatar.name}
            outfitName={room.outfit?.name}
            poses={room.catalog.poses}
            activePoseId={room.poseId}
            onPoseChange={room.setPoseId}
          />
          <PoseSelector
            poses={room.catalog.poses}
            activeId={room.poseId}
            onSelect={room.setPoseId}
          />
          {room.avatar.boothUrl && (
            <div className="mt-2">
              <BoothButton url={room.avatar.boothUrl} label={`${room.avatar.name} をBoothで見る`} />
            </div>
          )}
        </div>

        {/* 右: 衣装一覧（ジャンルフィルター + 縦スクロール） */}
        <div className="md:w-72 lg:w-80 flex-shrink-0 md:h-[calc(100vh-7rem)]">
          <OutfitList
            outfits={room.filteredOutfits}
            genres={room.catalog.genres}
            activeGenre={room.genre}
            onGenreSelect={room.setGenre}
            selectedId={room.outfitId}
            selectedOutfit={room.outfit}
            onSelect={room.toggleOutfit}
            showAllOutfits={room.showAllOutfits}
            onToggleShowAll={room.setShowAllOutfits}
            supportedCount={room.supportedCount}
            totalCount={room.avatar.outfits.length}
          />
        </div>
      </main>

      {/* 撮影ツール ダウンロード */}
      <div className="px-3 pb-8 max-w-[1400px] mx-auto w-full">
        <div className="bg-gradient-to-r from-sky-50 to-pink-50 rounded-2xl p-6 border border-pink-100">
          <h2 className="text-sm font-bold text-gray-600 mb-2">
            データを提供してくれる方へ
          </h2>
          <p className="text-xs text-gray-500 mb-4 leading-relaxed">
            Unity上でアバター×衣装の組み合わせを撮影できるツールです。<br />
            撮影データを提供していただくことで、データベースの充実にご協力いただけます。
          </p>
          <a
            href="/downloads/AvatarShooter.zip"
            download="AvatarShooter.zip"
            className="inline-flex items-center gap-2 bg-sky-400 hover:bg-sky-500 text-white text-sm font-medium px-5 py-2.5 rounded-full transition-colors shadow-sm"
          >
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
            </svg>
            撮影ツールをダウンロード（.zip）
          </a>
          <p className="text-[10px] text-gray-400 mt-2">
            AvatarShooter.unitypackage 入り — 解凍してUnityにインポートしてください
          </p>
        </div>
      </div>

      {/* フッター */}
      <footer className="border-t border-pink-100 bg-pink-50/30 py-4 px-3 mt-auto">
        <div className="max-w-[1400px] mx-auto flex flex-col sm:flex-row items-center justify-between gap-2 text-[10px] text-gray-400">
          <p>
            VRChat衣装データベース — 購入前にアバター×衣装の見た目をチェック
          </p>
          <p>
            衣装画像の著作権は各クリエイターに帰属します
          </p>
        </div>
      </footer>
    </div>
  );
}

// インラインのアバターサムネコンポーネント
function AvatarThumb({
  name,
  thumbnailUrl,
  isActive,
  onSelect,
}: {
  name: string;
  thumbnailUrl: string;
  isActive: boolean;
  onSelect: () => void;
}) {
  return (
    <button
      onClick={onSelect}
      className={`
        flex-shrink-0 w-20 md:w-full rounded-xl overflow-hidden transition-all border-2 group
        ${
          isActive
            ? "border-pink-400 shadow-md shadow-pink-200/50"
            : "border-transparent hover:border-pink-200 hover:shadow-sm"
        }
      `}
    >
      <div className="aspect-square bg-gradient-to-b from-sky-50 to-pink-50 relative overflow-hidden">
        {thumbnailUrl ? (
          <img
            src={thumbnailUrl}
            alt=""
            className="w-full h-full object-cover"
            loading="lazy"
            onError={(e) => { e.currentTarget.style.display = "none"; }}
          />
        ) : (
          <div className="w-full h-full flex items-center justify-center text-pink-200 text-xl">
            🎀
          </div>
        )}
      </div>
      <div className={`py-1 px-0.5 text-center ${isActive ? "bg-pink-50" : "bg-white"}`}>
        <p className={`text-[10px] font-medium leading-tight ${isActive ? "text-pink-500" : "text-gray-400 group-hover:text-pink-400"}`}>
          {name}
        </p>
      </div>
    </button>
  );
}
