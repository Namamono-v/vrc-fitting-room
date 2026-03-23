"use client";

import { useState } from "react";
import { Outfit } from "@/types";

interface Props {
  outfit: Outfit;
  isSelected: boolean;
  onSelect: () => void;
}

export function OutfitCard({ outfit, isSelected, onSelect }: Props) {
  const [imgError, setImgError] = useState(false);

  return (
    <button
      onClick={onSelect}
      className={`
        w-full text-left rounded-xl overflow-hidden transition-all border-2
        ${
          isSelected
            ? "border-pink-400 bg-pink-50 shadow-md shadow-pink-200/40"
            : "border-pink-100 bg-white hover:bg-pink-50 hover:border-pink-300"
        }
      `}
    >
      <div className="aspect-[4/3] bg-gradient-to-b from-sky-50/50 to-pink-50/50 relative overflow-hidden">
        {outfit.thumbnailUrl && !imgError ? (
          <img
            src={outfit.thumbnailUrl}
            alt=""
            className="w-full h-full object-cover"
            onError={() => setImgError(true)}
          />
        ) : (
          <div className="w-full h-full flex items-center justify-center text-pink-200 text-2xl">
            👗
          </div>
        )}
        {isSelected && (
          <div className="absolute top-1.5 right-1.5 w-5 h-5 bg-pink-400 rounded-full flex items-center justify-center z-20">
            <svg className="w-3 h-3 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={3}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
            </svg>
          </div>
        )}
        <div className="absolute bottom-1 left-1 bg-white/80 text-[9px] text-gray-400 px-1.5 py-0.5 rounded-full z-20">
          {outfit.genre}
        </div>
      </div>
      <div className="p-2">
        <p className="text-xs font-medium text-gray-600 leading-tight line-clamp-2">{outfit.name}</p>
        <p className="text-[10px] text-pink-400 font-medium mt-0.5">{outfit.price}</p>
      </div>
    </button>
  );
}
