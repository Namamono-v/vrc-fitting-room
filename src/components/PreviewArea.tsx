"use client";

import { useRef, useState, useEffect, useCallback } from "react";
import { Pose } from "@/types";

interface Props {
  src: string;
  avatarName: string;
  outfitName?: string;
  poses: Pose[];
  activePoseId: string;
  onPoseChange: (id: string) => void;
}

export function PreviewArea({
  src,
  avatarName,
  outfitName,
  poses,
  activePoseId,
  onPoseChange,
}: Props) {
  const imgRef = useRef<HTMLImageElement>(null);
  const [loaded, setLoaded] = useState(false);
  const [error, setError] = useState(false);

  useEffect(() => {
    setLoaded(false);
    setError(false);
  }, [src]);

  const checkComplete = useCallback(() => {
    if (imgRef.current?.complete && imgRef.current.naturalWidth > 0) {
      setLoaded(true);
    }
  }, []);

  useEffect(checkComplete, [src, checkComplete]);

  const currentIndex = poses.findIndex((p) => p.id === activePoseId);

  const goPrev = useCallback(() => {
    const prev = currentIndex <= 0 ? poses.length - 1 : currentIndex - 1;
    onPoseChange(poses[prev].id);
  }, [currentIndex, poses, onPoseChange]);

  const goNext = useCallback(() => {
    const next = currentIndex >= poses.length - 1 ? 0 : currentIndex + 1;
    onPoseChange(poses[next].id);
  }, [currentIndex, poses, onPoseChange]);

  return (
    <div
      className="relative w-full bg-white rounded-2xl overflow-hidden border border-pink-100 shadow-sm max-h-[70vh] group"
      style={{ aspectRatio: "3 / 4" }}
    >
      <img
        ref={imgRef}
        key={src}
        src={src}
        alt={outfitName ? `${avatarName} × ${outfitName}` : avatarName}
        className={`absolute inset-0 w-full h-full object-contain transition-opacity duration-200 ${
          loaded ? "opacity-100" : "opacity-0"
        }`}
        draggable={false}
        onLoad={() => setLoaded(true)}
        onError={() => setError(true)}
      />

      {!loaded && (
        <div className="absolute inset-0 flex items-center justify-center text-pink-300">
          <div className="text-center">
            <div className="text-5xl mb-3">🎀</div>
            <p className="text-sm text-pink-300">
              {error ? "画像準備中" : "読み込み中..."}
            </p>
          </div>
        </div>
      )}

      {/* ◀ 前へ */}
      {poses.length > 1 && (
        <button
          onClick={goPrev}
          className="absolute left-2 top-1/2 -translate-y-1/2 w-10 h-10 rounded-full bg-black/20 hover:bg-black/40 text-white flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity duration-200 backdrop-blur-sm"
          aria-label="前のポーズ"
        >
          <svg width="20" height="20" viewBox="0 0 20 20" fill="none">
            <path d="M12.5 15L7.5 10L12.5 5" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
          </svg>
        </button>
      )}

      {/* ▶ 次へ */}
      {poses.length > 1 && (
        <button
          onClick={goNext}
          className="absolute right-2 top-1/2 -translate-y-1/2 w-10 h-10 rounded-full bg-black/20 hover:bg-black/40 text-white flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity duration-200 backdrop-blur-sm"
          aria-label="次のポーズ"
        >
          <svg width="20" height="20" viewBox="0 0 20 20" fill="none">
            <path d="M7.5 5L12.5 10L7.5 15" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
          </svg>
        </button>
      )}

      {/* ドットインジケーター */}
      {poses.length > 1 && (
        <div className="absolute bottom-12 left-1/2 -translate-x-1/2 flex gap-1.5">
          {poses.map((pose, i) => (
            <button
              key={pose.id}
              onClick={() => onPoseChange(pose.id)}
              className={`w-2 h-2 rounded-full transition-all ${
                i === currentIndex
                  ? "bg-pink-400 w-4"
                  : "bg-white/60 hover:bg-white/90"
              }`}
              aria-label={pose.label}
            />
          ))}
        </div>
      )}

      {/* ラベル */}
      {outfitName && (
        <div className="absolute bottom-3 left-3 bg-white/80 text-pink-500 text-xs px-3 py-1.5 rounded-full backdrop-blur-sm border border-pink-200 font-medium">
          {outfitName}
        </div>
      )}

      {/* ポーズ名 */}
      {poses.length > 1 && (
        <div className="absolute top-3 right-3 bg-black/30 text-white text-[10px] px-2.5 py-1 rounded-full backdrop-blur-sm">
          {poses[currentIndex]?.label ?? ""}
        </div>
      )}
    </div>
  );
}
