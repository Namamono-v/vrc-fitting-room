"use client";

import { useState, useMemo } from "react";
import { catalog, getOutfitPosePath } from "@/data/catalog";
import { Avatar, Outfit } from "@/types";

const ADMIN_KEY = "zeroi chi2026";

export default function AdminPage() {
  const [authed, setAuthed] = useState(false);
  const [keyInput, setKeyInput] = useState("");
  const [changes, setChanges] = useState<Record<string, Record<string, { hidden?: boolean; deleted?: boolean }>>>({});
  const [saved, setSaved] = useState(false);

  if (!authed) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-gray-50">
        <div className="bg-white p-8 rounded-2xl shadow-lg max-w-sm w-full">
          <h1 className="text-lg font-bold text-gray-700 mb-4">管理画面</h1>
          <input
            type="password"
            value={keyInput}
            onChange={(e) => setKeyInput(e.target.value)}
            onKeyDown={(e) => { if (e.key === "Enter" && keyInput === ADMIN_KEY) setAuthed(true); }}
            placeholder="管理パスワード"
            className="w-full border border-gray-200 rounded-lg px-4 py-2 text-sm mb-3"
          />
          <button
            onClick={() => { if (keyInput === ADMIN_KEY) setAuthed(true); }}
            className="w-full bg-pink-400 text-white rounded-lg py-2 text-sm font-medium hover:bg-pink-500"
          >
            ログイン
          </button>
        </div>
      </div>
    );
  }

  const getStatus = (avatarId: string, outfitId: string) => {
    return changes[avatarId]?.[outfitId] ?? {};
  };

  const setStatus = (avatarId: string, outfitId: string, status: { hidden?: boolean; deleted?: boolean }) => {
    setChanges((prev) => ({
      ...prev,
      [avatarId]: {
        ...prev[avatarId],
        [outfitId]: status,
      },
    }));
    setSaved(false);
  };

  const exportCatalog = () => {
    const exported = {
      avatars: catalog.avatars.map((avatar) => ({
        id: avatar.id,
        name: avatar.name,
        boothUrl: avatar.boothUrl,
        thumbnailUrl: avatar.thumbnailUrl,
        supportedOutfitIds: avatar.supportedOutfitIds,
        outfits: avatar.outfits
          .filter((outfit) => !getStatus(avatar.id, outfit.id).deleted)
          .map((outfit) => ({
            id: outfit.id,
            name: outfit.name,
            boothUrl: outfit.boothUrl,
            creator: outfit.creator,
            price: outfit.price,
            genre: outfit.genre,
            thumbnailUrl: outfit.thumbnailUrl,
            contributors: outfit.contributors,
            ...(getStatus(avatar.id, outfit.id).hidden ? { hidden: true } : {}),
          })),
      })),
    };

    const blob = new Blob([JSON.stringify(exported, null, 2)], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = "catalog.json";
    a.click();
    URL.revokeObjectURL(url);
    setSaved(true);
  };

  const totalOutfits = catalog.avatars.reduce((sum, a) => sum + a.outfits.length, 0);
  const hiddenCount = Object.values(changes).reduce(
    (sum, av) => sum + Object.values(av).filter((s) => s.hidden).length, 0
  );
  const deletedCount = Object.values(changes).reduce(
    (sum, av) => sum + Object.values(av).filter((s) => s.deleted).length, 0
  );

  return (
    <div className="min-h-screen bg-gray-50">
      {/* ヘッダー */}
      <header className="bg-white border-b border-gray-200 px-6 py-4 sticky top-0 z-50">
        <div className="max-w-[1400px] mx-auto flex items-center justify-between">
          <div>
            <h1 className="text-lg font-bold text-gray-700">管理画面</h1>
            <p className="text-xs text-gray-400">
              {catalog.avatars.length}体 / {totalOutfits}衣装
              {hiddenCount > 0 && <span className="text-yellow-500 ml-2">非表示: {hiddenCount}</span>}
              {deletedCount > 0 && <span className="text-red-500 ml-2">削除: {deletedCount}</span>}
            </p>
          </div>
          <button
            onClick={exportCatalog}
            className={`px-5 py-2 rounded-full text-sm font-medium transition-all ${
              saved
                ? "bg-green-400 text-white"
                : "bg-pink-400 hover:bg-pink-500 text-white shadow-md"
            }`}
          >
            {saved ? "ダウンロード済み" : "catalog.json をダウンロード"}
          </button>
        </div>
      </header>

      {/* メインコンテンツ */}
      <main className="max-w-[1400px] mx-auto px-6 py-6">
        {catalog.avatars.map((avatar) => (
          <AvatarSection
            key={avatar.id}
            avatar={avatar}
            changes={changes[avatar.id] ?? {}}
            onStatusChange={(outfitId, status) => setStatus(avatar.id, outfitId, status)}
          />
        ))}
      </main>
    </div>
  );
}

function AvatarSection({
  avatar,
  changes,
  onStatusChange,
}: {
  avatar: Avatar;
  changes: Record<string, { hidden?: boolean; deleted?: boolean }>;
  onStatusChange: (outfitId: string, status: { hidden?: boolean; deleted?: boolean }) => void;
}) {
  return (
    <div className="mb-8">
      <div className="flex items-center gap-3 mb-4">
        <img
          src={avatar.thumbnailUrl}
          alt=""
          className="w-12 h-12 rounded-lg object-cover bg-gray-100"
          onError={(e) => { e.currentTarget.style.display = "none"; }}
        />
        <div>
          <h2 className="text-sm font-bold text-gray-700">{avatar.name}</h2>
          <p className="text-[10px] text-gray-400">{avatar.id} / {avatar.outfits.length}衣装</p>
        </div>
      </div>

      <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6 gap-3">
        {avatar.outfits.map((outfit) => {
          const status = changes[outfit.id] ?? {};
          return (
            <OutfitAdminCard
              key={outfit.id}
              avatarId={avatar.id}
              outfit={outfit}
              hidden={!!status.hidden}
              deleted={!!status.deleted}
              onToggleHidden={() => onStatusChange(outfit.id, { ...status, hidden: !status.hidden, deleted: false })}
              onToggleDeleted={() => onStatusChange(outfit.id, { ...status, deleted: !status.deleted, hidden: false })}
            />
          );
        })}
      </div>
    </div>
  );
}

function OutfitAdminCard({
  avatarId,
  outfit,
  hidden,
  deleted,
  onToggleHidden,
  onToggleDeleted,
}: {
  avatarId: string;
  outfit: Outfit;
  hidden: boolean;
  deleted: boolean;
  onToggleHidden: () => void;
  onToggleDeleted: () => void;
}) {
  const previewSrc = getOutfitPosePath(avatarId, outfit.id, "arms-crossed");

  return (
    <div
      className={`rounded-xl border-2 overflow-hidden transition-all ${
        deleted
          ? "border-red-300 opacity-40"
          : hidden
          ? "border-yellow-300 opacity-60"
          : "border-gray-200"
      }`}
    >
      {/* サムネイル */}
      <div className="aspect-[3/4] bg-gray-100 relative">
        <img
          src={previewSrc}
          alt=""
          className="w-full h-full object-contain"
          onError={(e) => {
            e.currentTarget.style.display = "none";
          }}
        />
        {/* ステータスバッジ */}
        {deleted && (
          <div className="absolute top-1 left-1 bg-red-500 text-white text-[9px] px-2 py-0.5 rounded-full">
            削除
          </div>
        )}
        {hidden && !deleted && (
          <div className="absolute top-1 left-1 bg-yellow-500 text-white text-[9px] px-2 py-0.5 rounded-full">
            非表示
          </div>
        )}
      </div>

      {/* 情報 */}
      <div className="p-2">
        <p className="text-[11px] font-medium text-gray-600 line-clamp-1">{outfit.name}</p>
        <p className="text-[9px] text-gray-400">{outfit.genre}</p>
        {outfit.contributors && outfit.contributors.length > 0 && (
          <p className="text-[9px] text-gray-400">
            提供: {outfit.contributors.filter(c => c !== "匿名")[0] ?? "匿名"}
          </p>
        )}
      </div>

      {/* アクションボタン */}
      <div className="flex border-t border-gray-100">
        <button
          onClick={onToggleHidden}
          className={`flex-1 text-[10px] py-1.5 transition-colors ${
            hidden ? "bg-yellow-50 text-yellow-600 font-medium" : "text-gray-400 hover:bg-yellow-50"
          }`}
        >
          {hidden ? "表示する" : "非表示"}
        </button>
        <button
          onClick={onToggleDeleted}
          className={`flex-1 text-[10px] py-1.5 border-l border-gray-100 transition-colors ${
            deleted ? "bg-red-50 text-red-600 font-medium" : "text-gray-400 hover:bg-red-50"
          }`}
        >
          {deleted ? "復元" : "削除"}
        </button>
      </div>
    </div>
  );
}
