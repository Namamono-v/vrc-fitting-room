"use client";

import { Outfit, OutfitGenre } from "@/types";
import { OutfitCard } from "./OutfitCard";
import { GenreFilter } from "./GenreFilter";
import { BoothButton } from "./BoothButton";

interface Props {
  outfits: Outfit[];
  genres: { id: OutfitGenre; label: string }[];
  activeGenre: OutfitGenre;
  onGenreSelect: (genre: OutfitGenre) => void;
  selectedId: string | null;
  selectedOutfit: Outfit | null;
  onSelect: (id: string) => void;
  showAllOutfits: boolean;
  onToggleShowAll: (show: boolean) => void;
  supportedCount: number;
  totalCount: number;
}

export function OutfitList({
  outfits,
  genres,
  activeGenre,
  onGenreSelect,
  selectedId,
  selectedOutfit,
  onSelect,
  showAllOutfits,
  onToggleShowAll,
  supportedCount,
  totalCount,
}: Props) {
  return (
    <div className="flex flex-col h-full gap-3">
      {/* ヘッダー */}
      <div className="flex-shrink-0">
        <div className="flex items-center justify-between mb-2">
          <h2 className="text-xs font-bold text-gray-400 uppercase tracking-wider">
            衣装一覧
            <span className="text-pink-300 font-normal ml-1 normal-case">{outfits.length}着</span>
          </h2>
          <span className="text-[10px] text-gray-400">
            対応 {supportedCount}/{totalCount}
          </span>
        </div>

        {/* 対応/全表示 トグル */}
        <div className="flex items-center gap-2 mb-2">
          <button
            onClick={() => onToggleShowAll(false)}
            className={`text-[11px] px-2.5 py-1 rounded-full transition-all ${
              !showAllOutfits
                ? "bg-pink-400 text-white"
                : "bg-white text-gray-400 border border-pink-100 hover:bg-pink-50"
            }`}
          >
            対応のみ
          </button>
          <button
            onClick={() => onToggleShowAll(true)}
            className={`text-[11px] px-2.5 py-1 rounded-full transition-all ${
              showAllOutfits
                ? "bg-pink-400 text-white"
                : "bg-white text-gray-400 border border-pink-100 hover:bg-pink-50"
            }`}
          >
            すべて
          </button>
        </div>

        {/* ジャンルフィルター */}
        <GenreFilter
          genres={genres}
          activeGenre={activeGenre}
          onSelect={onGenreSelect}
        />
      </div>

      {/* 衣装グリッド */}
      <div className="grid grid-cols-2 gap-2 overflow-y-auto flex-1 pr-1 auto-rows-min">
        {outfits.map((outfit) => (
            <OutfitCard
              key={outfit.id}
              outfit={outfit}
              isSelected={selectedId === outfit.id}
              onSelect={() => onSelect(outfit.id)}
            />
        ))}
        {outfits.length === 0 && (
          <div className="col-span-2 text-center py-10">
            <div className="text-3xl mb-2 text-pink-200">
              {activeGenre !== "all" ? "🔍" : "👗"}
            </div>
            <p className="text-sm text-gray-400 font-medium">
              {activeGenre !== "all"
                ? "このジャンルの衣装はありません"
                : showAllOutfits
                ? "登録されている衣装がありません"
                : "対応衣装がまだありません"}
            </p>
            {activeGenre !== "all" && (
              <button
                onClick={() => onGenreSelect("all" as OutfitGenre)}
                className="mt-2 text-xs text-pink-400 hover:text-pink-500 underline underline-offset-2"
              >
                すべてのジャンルを表示
              </button>
            )}
            {!showAllOutfits && activeGenre === "all" && (
              <button
                onClick={() => onToggleShowAll(true)}
                className="mt-2 text-xs text-pink-400 hover:text-pink-500 underline underline-offset-2"
              >
                すべての衣装を表示
              </button>
            )}
          </div>
        )}
      </div>

      {/* Boothリンク */}
      {selectedOutfit && (
        <div className="flex-shrink-0 pt-3 border-t border-pink-100">
          <p className="text-xs text-gray-400 mb-2">
            {selectedOutfit.name} / {selectedOutfit.creator}
          </p>
          <BoothButton url={selectedOutfit.boothUrl} price={selectedOutfit.price} />
        </div>
      )}
    </div>
  );
}
