// Figma's plugin VM need not expose browser TextEncoder. The shared metadata
// validator only uses UTF-8 byte length, but this shim encodes full UTF-8 bytes.
if(typeof TextEncoder==='undefined')globalThis.TextEncoder=class TextEncoder{
 encode(input=''){const bytes=[];for(const character of String(input)){let cp=character.codePointAt(0);if(cp>=0xD800&&cp<=0xDFFF)cp=0xFFFD;if(cp<0x80)bytes.push(cp);else if(cp<0x800)bytes.push(0xC0|(cp>>6),0x80|(cp&63));else if(cp<0x10000)bytes.push(0xE0|(cp>>12),0x80|((cp>>6)&63),0x80|(cp&63));else bytes.push(0xF0|(cp>>18),0x80|((cp>>12)&63),0x80|((cp>>6)&63),0x80|(cp&63));}return new Uint8Array(bytes);}
};
