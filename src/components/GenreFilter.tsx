"use client";

import { OutfitGenre } from "@/types";

interface Props {
  genres: { id: OutfitGenre; label: string }[];
  activeGenre: OutfitGenre;
  onSelect: (genre: OutfitGenre) => void;
}

export function GenreFilter({ genres, activeGenre, onSelect }: Props) {
  return (
    <div className="flex gap-1.5 overflow-x-auto pb-1 flex-shrink-0">
      {genres.map((g) => (
        <button
          key={g.id}
          onClick={() => onSelect(g.id)}
          className={`
            flex-shrink-0 px-3 py-1 rounded-full text-[11px] font-medium transition-all
            ${
              activeGenre === g.id
                ? "bg-pink-400 text-white shadow-sm"
                : "bg-white text-gray-400 hover:bg-pink-50 hover:text-pink-400 border border-pink-100"
            }
          `}
        >
          {g.label}
        </button>
      ))}
    </div>
  );
}
