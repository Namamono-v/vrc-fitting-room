interface Props {
  url: string;
  price?: string;
}

export function BoothButton({ url, price }: Props) {
  return (
    <a
      href={url}
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
