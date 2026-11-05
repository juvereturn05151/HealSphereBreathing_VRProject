import { createHash } from 'node:crypto';

const groups = {
  category: { gorilla: ['gorilla', 'gorillas'], fox: ['fox', 'foxes'], cat: ['cat', 'kitten', 'tabby'], rabbit: ['rabbit', 'bunny'], tree: ['tree'], moon: ['moon', 'crescent'], dragon: ['dragon'], dog: ['dog', 'puppy'], flower: ['flower'], star: ['star'], mushroom: ['mushroom'], turtle: ['turtle'], bird: ['bird'], fish: ['fish'], butterfly: ['butterfly'] },
  color: { orange: ['orange', 'ginger'], blue: ['blue', 'azure'], purple: ['purple', 'violet'], red: ['red'], pink: ['pink'], white: ['white'], black: ['black'], brown: ['brown'], green: ['green'], yellow: ['yellow'], gray: ['gray', 'grey'], cream: ['cream'] },
  pose: { sleeping: ['taking a nap', 'napping', 'sleeping', 'asleep', 'sleep', 'snoozing'], curled: ['curled', 'curling'], seated: ['seated', 'sitting'], standing: ['standing'], flying: ['flying'], walking: ['walking'], lying: ['lying', 'laying'] },
  anatomy: { wings: ['winged', 'wings'], horns: ['horned', 'horns', 'horn'], extra_heads: ['two headed', 'three headed'], extra_legs: ['six legs', 'eight legs'] },
  accessories: { hat: ['hat', 'cap'], bow: ['bow', 'ribbon'], glasses: ['glasses', 'spectacles'], scarf: ['scarf'], crown: ['crown'], collar: ['collar'], armor: ['armor', 'armour'] },
  style: { low_poly: ['low poly', 'lowpoly'], realistic: ['realistic', 'photorealistic'], stylized: ['stylized', 'stylised', 'cartoon'], pastel: ['pastel'] },
  material: { wood: ['wooden', 'wood'], metal: ['metal', 'metallic'], glass: ['glass'], crystal: ['crystal'], stone: ['stone'], plush: ['plush', 'stuffed'] },
  texture: { fluffy: ['fluffy', 'furry'], striped: ['striped', 'stripes', 'tabby'], spotted: ['spotted', 'spots'] }
  ,age: { baby: ['baby', 'young'], adult: ['adult'] }
  ,size: { large: ['large', 'giant', 'big'], chubby: ['fat', 'chubby'] }
};
const stop = new Set('can you i want would like wish for please make create generate a an the little small cute peaceful peacefully gently gentle friendly relaxing calm calming cozy very soft rounded body complete fur tail expression model asset game 3d object one with and up in of to it me my is that something environment wellness suitable experience colors colour color baby tiny adorable'.split(' '));
export function normalize(text) { return String(text ?? '').normalize('NFKC').toLowerCase().replace(/[^a-z0-9 ]/g, ' ').replace(/\s+/g, ' ').trim(); }
export function requirements(text) {
  let remaining = ' ' + normalize(text) + ' ';
  const result = {};
  for (const [field, dictionary] of Object.entries(groups)) {
    result[field] = [];
    for (const [canonical, words] of Object.entries(dictionary)) for (const word of words) {
      const expression = new RegExp('\\b' + word + '\\b', 'g');
      if (expression.test(remaining)) { if (!result[field].includes(canonical)) result[field].push(canonical); remaining = remaining.replace(expression, ' '); }
    }
  }
  result.unknown = [...new Set(remaining.trim().split(/\s+/).filter(x => x && !stop.has(x)))].sort();
  result.conservative = !/\b(no|not|without|except|instead|only|multiple|pair|two|three)\b/.test(normalize(text));
  for (const values of Object.values(result)) if (Array.isArray(values)) values.sort();
  return result;
}
export function requestKey(text) { return createHash('sha256').update(JSON.stringify(requirements(text))).digest('hex'); }
export function satisfies(request, asset) {
  if (!request.conservative || request.category.length !== 1 || asset.category.length !== 1 || request.category[0] !== asset.category[0]) return false;
  if (request.color.length > 1 || request.color.length > 0 && asset.color.length > 1) return false;
  for (const field of Object.keys(groups)) if (request[field].some(value => !asset[field]?.includes(value))) return false;
  // Do not quietly add wings, horns, hats or other prominent features to a plain request.
  for (const field of ['anatomy', 'accessories']) if (asset[field]?.some(value => !request[field].includes(value))) return false;
  if (request.unknown.some(value => !asset.unknown?.includes(value))) return false;
  return true;
}
