"use client";

import { useState } from "react";
import { Avatar } from "@/types";

interface Props {
  avatars: Avatar[];
  activeId: string;
  onSelect: (id: string) => void;
}

export function AvatarSelector({ avatars, activeId, onSelect }: Props) {
  return (
    <div className="flex flex-col gap-2 overflow-y-auto pr-1 h-full">
      <h2 className="text-xs font-bold text-gray-400 uppercase tracking-wider mb-1 sticky top-0 bg-pink-50/90 backdrop-blur-sm py-1 z-10">
        アバター
      </h2>
      {avatars.map((avatar) => (
        <AvatarThumb
          key={avatar.id}
          avatar={avatar}
          isActive={activeId === avatar.id}
          onSelect={() => onSelect(avatar.id)}
        />
      ))}
    </div>
  );
}

function AvatarThumb({
  avatar,
  isActive,
  onSelect,
}: {
  avatar: Avatar;
  isActive: boolean;
  onSelect: () => void;
}) {
  const [imgError, setImgError] = useState(false);

  return (
    <button
      onClick={onSelect}
      className={`
        flex-shrink-0 rounded-xl overflow-hidden transition-all border-2 group
        ${
          isActive
            ? "border-pink-400 shadow-md shadow-pink-200/50 scale-[1.02]"
            : "border-transparent hover:border-pink-200 hover:shadow-sm"
        }
      `}
    >
      {/* サムネイル */}
      <div className="aspect-square bg-gradient-to-b from-sky-50 to-pink-50 relative overflow-hidden">
        {!imgError && avatar.thumbnailUrl ? (
          <img
            src={avatar.thumbnailUrl}
            alt=""
            className="w-full h-full object-cover"
            loading="lazy"
            onError={() => setImgError(true)}
          />
        ) : (
          <div className="w-full h-full flex items-center justify-center text-pink-200 text-2xl">
            🎀
          </div>
        )}
        {isActive && (
          <div className="absolute inset-0 border-2 border-pink-400 rounded-[10px]" />
        )}
      </div>
      {/* 名前 */}
      <div className={`py-1.5 px-1 text-center ${isActive ? "bg-pink-50" : "bg-white"}`}>
        <p className={`text-[10px] font-medium leading-tight ${isActive ? "text-pink-500" : "text-gray-500 group-hover:text-pink-400"}`}>
          {avatar.name}
        </p>
      </div>
    </button>
  );
}
