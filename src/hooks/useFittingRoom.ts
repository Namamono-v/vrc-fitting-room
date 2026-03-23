"use client";

import { useState, useEffect, useCallback, useMemo } from "react";
import { Catalog, OutfitGenre } from "@/types";
import { getOutfitPosePath } from "@/data/catalog";

export function useFittingRoom(catalog: Catalog) {
  const [avatarId, setAvatarId] = useState(catalog.avatars[0].id);
  const firstOutfitId = catalog.avatars[0]?.outfits[0]?.id ?? null;
  const [outfitId, setOutfitId] = useState<string | null>(firstOutfitId);
  const [poseId, setPoseId] = useState("arms-crossed");
  const [genre, setGenre] = useState<OutfitGenre>("all");
  const [showAllOutfits, setShowAllOutfits] = useState(false);

  const avatar = catalog.avatars.find((a) => a.id === avatarId)!;
  const outfit = outfitId
    ? avatar.outfits.find((o) => o.id === outfitId) ?? null
    : null;

  const filteredOutfits = useMemo(() => {
    let outfits = avatar.outfits;

    if (!showAllOutfits && avatar.supportedOutfitIds) {
      outfits = outfits.filter((o) => avatar.supportedOutfitIds!.includes(o.id));
    }

    if (genre !== "all") {
      outfits = outfits.filter((o) => o.genre === genre);
    }

    return outfits;
  }, [avatar, genre, showAllOutfits]);

  const supportedCount = avatar.supportedOutfitIds
    ? avatar.supportedOutfitIds.length
    : avatar.outfits.length;

  // ポーズ対応パス生成
  // front(正面) → {outfitId}.png (従来通り)
  // それ以外 → {outfitId}_{poseId}.png
  const previewSrc = useMemo(() => {
    return outfitId
      ? getOutfitPosePath(avatarId, outfitId, poseId)
      : `/renders/${avatarId}/素体_${poseId}.png`;
  }, [avatarId, outfitId, poseId]);

  useEffect(() => {
    const newAvatar = catalog.avatars.find((a) => a.id === avatarId);
    setOutfitId(newAvatar?.outfits[0]?.id ?? null);
    setGenre("all");
  }, [avatarId, catalog.avatars]);

  const toggleOutfit = useCallback((id: string) => {
    setOutfitId((prev) => (prev === id ? null : id));
  }, []);

  const isOutfitSupported = useCallback(
    (outfitIdToCheck: string) => {
      if (!avatar.supportedOutfitIds) return true;
      return avatar.supportedOutfitIds.includes(outfitIdToCheck);
    },
    [avatar]
  );

  return {
    avatarId, setAvatarId,
    outfitId, toggleOutfit,
    poseId, setPoseId,
    genre, setGenre,
    showAllOutfits, setShowAllOutfits,
    avatar, outfit,
    filteredOutfits,
    supportedCount,
    isOutfitSupported,
    previewSrc,
    catalog,
  };
}
