const sharp = (await import(process.argv[2] || 'sharp')).default;
for (const f of ['Billboard/Billboard.png','Billboard/Chains/Chain_1.png','Billboard/Chains/Chain_2.png']) console.log(f,await sharp('Models_Site/'+f).metadata());
