interface Props {
  url: string;
  price?: string;
}

function isSafeUrl(url: string): boolean {
  try {
    const u = new URL(url);
    return u.protocol === "https:" || u.protocol === "http:";
  } catch { return false; }
}

export function BoothButton({ url, price }: Props) {
  const safeUrl = isSafeUrl(url) ? url : "#";
  return (
    <a
      href={safeUrl}
      target="_blank"
      rel="noopener noreferrer"
      className="
        inline-flex items-center gap-2 px-4 py-2.5 rounded-full text-sm font-medium
        bg-gradient-to-r from-pink-400 to-pink-500 text-white
        hover:from-pink-500 hover:to-pink-600
        transition-all shadow-md shadow-pink-300/30 w-full justify-center
      "
    >
      <span>Boothで見る</span>
      {price && <span className="opacity-90">{price}</span>}
    </a>
  );
}
