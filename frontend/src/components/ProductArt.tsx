import type { Product } from '../api/types';

/** Category glyphs (24x24 stroke icons) used for generated product artwork. */
const ICONS: Record<string, string> = {
  apparel: 'M8 3 4 6l2 4 2-1v12h8V9l2 1 2-4-4-3a4 4 0 0 1-8 0Z',
  footwear: 'M3 15V9l5 1 3-3 2 4c3 1 6 1.5 8 3v3H3Zm0 2h18',
  electronics: 'M4 14v-2a8 8 0 0 1 16 0v2M4 14h3v6H4zM17 14h3v6h-3z',
  'home-kitchen': 'M5 8h12v6a6 6 0 0 1-12 0V8Zm12 2h1.5a2.5 2.5 0 0 1 0 5H16M8 3v2M11 3v2M14 3v2',
  outdoor: 'M12 3 2 21h20L12 3Zm0 8-4 10m4-10 4 10',
};

const PALETTES = [
  ['#6366f1', '#a855f7'],
  ['#0ea5e9', '#22d3ee'],
  ['#f97316', '#f43f5e'],
  ['#10b981', '#84cc16'],
  ['#8b5cf6', '#ec4899'],
  ['#14b8a6', '#3b82f6'],
] as const;

function categoryKey(category: string) {
  const c = category.toLowerCase();
  if (c.includes('foot')) return 'footwear';
  if (c.includes('electr')) return 'electronics';
  if (c.includes('home') || c.includes('kitchen')) return 'home-kitchen';
  if (c.includes('outdoor')) return 'outdoor';
  return 'apparel';
}

interface Props {
  product: Pick<Product, 'id' | 'name' | 'category' | 'imageUrl'>;
  size?: 'sm' | 'md' | 'lg';
}

export function ProductArt({ product, size = 'md' }: Props) {
  if (product.imageUrl) {
    return <img className={`product-art product-art--${size}`} src={product.imageUrl} alt={product.name} loading="lazy" />;
  }
  const [from, to] = PALETTES[product.id % PALETTES.length]!;
  return (
    <div
      className={`product-art product-art--${size}`}
      style={{ background: `linear-gradient(135deg, ${from}, ${to})` }}
      role="img"
      aria-label={product.name}
    >
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.4" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
        <path d={ICONS[categoryKey(product.category)]} />
      </svg>
    </div>
  );
}
