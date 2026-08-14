import Link from "next/link";

/**
 * The mark is a token with an origin point and a rise cut out of it in negative
 * space. It deliberately does not span corner to corner: a full diagonal through
 * a rounded square reads as the universal "prohibited" symbol, which is the last
 * thing a lending product should put next to its name.
 */
export function BrandHeader() {
  return (
    <header className="px-6 pt-6">
      <Link
        href="/"
        className="inline-flex items-center gap-2.5 rounded-md outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50"
      >
        <svg viewBox="0 0 32 32" className="size-7 shrink-0" aria-hidden="true">
          <rect width="32" height="32" rx="9" className="fill-foreground" />
          <circle cx="10.5" cy="21.5" r="2.25" className="fill-background" />
          <path
            d="M14.5 17.5 22 10"
            className="stroke-background"
            strokeWidth="3"
            strokeLinecap="round"
          />
        </svg>
        <span className="text-lg font-semibold tracking-tight">Fundo</span>
      </Link>
    </header>
  );
}
