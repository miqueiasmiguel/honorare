// Valid steps match $spacing-scale in src/styles/tokens/_spacing.scss
const VALID_STEPS = new Set([1, 2, 3, 4, 6, 8, 12, 16, 24]);
const ruleName = 'honorare/valid-space-step';
const meta = { url: '' };

function rule(primaryOption) {
  return (root, result) => {
    if (primaryOption === false) return;

    root.walkDecls((decl) => {
      const pattern = /\bspace\((\d+)\)/g;
      let match;
      while ((match = pattern.exec(decl.value)) !== null) {
        const step = Number(match[1]);
        if (!VALID_STEPS.has(step)) {
          result.warn(
            `Invalid spacing step ${step}. Valid: 1(4px) 2(8px) 3(12px) 4(16px) 6(24px) 8(32px) 12(48px) 16(64px) 24(96px)`,
            { node: decl, rule: ruleName },
          );
        }
      }
    });
  };
}

export default { ruleName, rule, meta };
