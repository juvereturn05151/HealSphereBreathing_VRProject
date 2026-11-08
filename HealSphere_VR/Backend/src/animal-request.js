import { safeText } from './conversation.js';
import { normalize, requirements } from './matching.js';

const subjects = new Set('gorilla fox cat rabbit dragon dog turtle bird fish butterfly elephant deer panda bear otter horse dolphin owl lion red_panda'.split(' '));
const blocked = /\b(and|or|with|beside|inside|near|riding|holding|background|scene|group|pack|pair|two|three|multiple|ignore|instructions)\b/i;

export function prepareAnimal(input) {
  if (!safeText(input, 120) || blocked.test(input) || /[^a-z0-9 .'-]/i.test(input)) return null;
  const description = normalize(input).replace(/^(?:a|an|the) /, '');
  const attributes = requirements(description);
  if (attributes.category.length !== 1 || !subjects.has(attributes.category[0]) ||
      !attributes.conservative || attributes.anatomy.length || attributes.accessories.length ||
      attributes.expression.includes('angry') ||
      attributes.unknown.some(word => !['playful', 'resting', 'curious', 'happy', 'perched'].includes(word))) return null;
  const words = description.match(/\b(?:cats?|kittens?|tabby|dogs?|puppy|fox(?:es)?|rabbits?|bunny|gorillas?|dragon|turtle|bird|fish|butterfly|elephant|deer|panda|bear|otter|horse|dolphin|owl|lion)\b/g);
  if (words?.length !== 1) return null;
  return {
    description,
    displayName: description[0].toUpperCase() + description.slice(1),
    prompt: `One ${description}. Complete-body cute peaceful animal, rounded features, soft pastel palette, stylized low-poly VR character, isolated on a grounded base, no text or scenery.`
  };
}
