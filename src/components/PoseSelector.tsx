"use client";

import { Pose } from "@/types";

interface Props {
  poses: Pose[];
  activeId: string;
  onSelect: (id: string) => void;
}

export function PoseSelector({ poses, activeId, onSelect }: Props) {
  return (
    <div className="flex gap-1.5 mt-3 overflow-x-auto pb-1 scrollbar-thin">
      {poses.map((pose) => (
        <button
          key={pose.id}
          onClick={() => onSelect(pose.id)}
          className={`
            shrink-0 px-3 py-1.5 rounded-full text-xs font-medium transition-all
            ${
              activeId === pose.id
                ? "bg-sky-300 text-white shadow-sm"
                : "bg-white text-gray-400 hover:bg-sky-50 hover:text-sky-400 border border-sky-200"
            }
          `}
        >
          {pose.label}
        </button>
      ))}
    </div>
  );
}
