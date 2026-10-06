const sharp=require('sharp'),path=require('path');
const root=path.resolve(__dirname,'../../Assets/ArtAssets/Characters/Enemies/Ambusher');
(async()=>{
 const source=path.join(root,'Source/LeapGrab/GeneratedSheet.png');const meta=await sharp(source).metadata();
 console.log(meta.width,meta.height,meta.hasAlpha);
 const names=['Telegraph','DeepCrouch','Takeoff','Rising','Airborne','DescendingReach','Hold','Windup','FaceImpact','ReturnHold','Release','Landing'];
 for(let i=0;i<12;i++){
  const x=Math.round(i%4*meta.width/4),y=Math.round(Math.floor(i/4)*meta.height/3);
  const right=Math.round((i%4+1)*meta.width/4),bottom=Math.round((Math.floor(i/4)+1)*meta.height/3);
  const cell=await sharp(source).extract({left:x,top:y,width:right-x,height:bottom-y}).toBuffer(); const crop=await sharp(cell).trim().toBuffer();
  // One common pixel scale preserves the lower crouch and tucked airborne silhouette.
  const sourceSize=await sharp(crop).metadata(); const resized=await sharp(crop).resize({height:Math.round(sourceSize.height*.34),width:Math.round(sourceSize.width*.34),kernel:'nearest'}).toBuffer();const m=await sharp(resized).metadata();
  await sharp({create:{width:128,height:128,channels:4,background:{r:0,g:0,b:0,alpha:0}}}).composite([{input:resized,left:Math.floor((128-m.width)/2),top:120-m.height}]).png().toFile(path.join(root,`Animations/LeapGrab/Ambusher_${String(i+1).padStart(2,'0')}_${names[i]}.png`));
 }
})();
