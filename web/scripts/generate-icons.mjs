/**
 * Icon generation script. Renders public/favicon.svg into the PNG sizes required by
 * browser PWA installation and the iOS home screen. Run after modifying the source SVG:
 *
 *   npm install --no-save sharp
 *   node scripts/generate-icons.mjs
 *
 * The generated PNGs are committed to source control; sharp is not a project dependency.
 */
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import sharp from 'sharp';

const here = dirname(fileURLToPath(import.meta.url));
const root = resolve(here, '..');
const src = join(root, 'public', 'favicon.svg');
const outDir = join(root, 'public', 'icons');

const targets = [
  { name: 'icon-192.png', size: 192 },
  { name: 'icon-512.png', size: 512 },
  // Maskable variant with safe-area padding for Android adaptive icon masks.
  { name: 'icon-maskable-512.png', size: 512, padding: 0.1 },
  // Apple touch icon. iOS does not honour SVG icons for the home screen.
  { name: 'apple-touch-icon.png', size: 180 },
];

await mkdir(outDir, { recursive: true });
const svg = await readFile(src);

for (const { name, size, padding = 0 } of targets) {
  const inner = Math.round(size * (1 - padding * 2));
  const offset = Math.round((size - inner) / 2);
  const rendered = await sharp(svg, { density: 384 })
    .resize(inner, inner, { fit: 'contain' })
    .png()
    .toBuffer();
  const out = await sharp({
    create: {
      width: size,
      height: size,
      channels: 4,
      background: { r: 0xFA, g: 0xF7, b: 0xF2, alpha: 1 },
    },
  })
    .composite([{ input: rendered, left: offset, top: offset }])
    .png()
    .toBuffer();
  await writeFile(join(outDir, name), out);
  console.log(`wrote public/icons/${name} (${size}×${size})`);
}
