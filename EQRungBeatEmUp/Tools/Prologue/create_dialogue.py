"""Seed bilingual story content; designers edit the generated Unity database thereafter."""
from pathlib import Path
import json
ROOT=Path(__file__).resolve().parents[2]
conversations=[]
def add(id,lines):
    conversations.append(dict(id=id,lines=[dict(speaker=s,thai=th,english=en,charactersPerSecond=35,choices=[]) for s,th,en in lines]))
add('reunion',[
('EQ','เฮ้ย! รุ่ง! นั่นมึงจริง ๆ เหรอวะ?','Hey! Rung! Is that really you?'),
('Rung','ไอ้อีคิว! ไม่เจอกันตั้ง 16 ปี!','EQ! Sixteen years since we last met!'),
('EQ','โห แก่ขึ้นเยอะเลยนะมึง!','You look a lot older!'),
('Rung','มึงก็ไม่ต่างกันหรอก!','So do you!'),
('EQ','ไปเดินงานกันเถอะเพื่อน!','Let’s walk around the fair, my friend!'),
('Rung','เออ ไปดิ!','Yeah, let’s go!')])
add('chanai-fair',[
('Rung','เฮ้ย... ทำไมทุกคนยืนนิ่งกันหมดวะ?','Hey… why is everyone standing still?'),
('EQ','กูว่ามีอะไรแปลก ๆ แล้วว่ะ','Something is wrong here.'),
('Chanai','ผ่านมาตั้ง 16 ปี... พวกเจ้ายังมีชีวิตอยู่อีกหรือ?','Sixteen years have passed… you two are still alive?'),
('EQ','มึงเป็นใครวะ!?','Who the hell are you!?'),
('Chanai','หึ... จำข้าไม่ได้จริง ๆ สินะ','Heh… you really don’t remember me.'),
('Rung','กูไม่รู้จักมึง! ทำอะไรกับพวกเด็กวะ!?','We don’t know you! What did you do to the students!?'),
('Chanai','น่าสนใจ... ทำไมพวกเจ้าถึงยังมีสติ?','Interesting… why are your minds still intact?')])
add('cornered',[
('EQ','ซวยแล้ว! ทางตัน!','Damn! A dead end!'),
('Rung','จะให้สู้กับเด็กทั้งโรงเรียนเลยหรือไงวะ!','Are we supposed to fight the whole school!?')])
add('sanctuary-seal',[
('Prapot','ในที่สุด... พวกเธอสองคนก็มาถึง','At last… you two have arrived.'),
('EQ','อาจารย์ประพจน์!?','Professor Prapot!?'),
('Rung','อาจารย์มาอยู่ที่นี่ได้ไงครับ!?','How did you get here, professor!?'),
('Prapot','พวกเธอจำเรื่องเมื่อ 16 ปีก่อนไม่ได้สินะ','You don’t remember what happened sixteen years ago, do you?'),
('EQ','เรื่องอะไรครับ?','What happened?'),
('Prapot','เรื่องของครีม... และพลังที่พวกเธอเคยได้รับ','Cream… and the powers you once received.'),
('Rung','พลัง? พวกผมเนี่ยนะ?','Powers? Us?'),
('Prapot','ความทรงจำของพวกเธอไม่ได้หายไปเอง...','Your memories did not disappear on their own…'),
('Prapot','มีใครบางคนจงใจลบมันออกไป','Someone deliberately erased them.'),
('Prapot','เวทมนตร์อันทรงพลังผนึกความทรงจำเหล่านั้นไว้','Powerful magic sealed those memories away.'),
('Prapot','ถึงเวลาที่พวกเธอต้องจำอดีตของตัวเองแล้ว','It is time for you to remember your past.')])
add('flashback-go',[
('EQ','ไปกันเถอะเพื่อน!','Let’s go, my friend!'),
('Rung','เออ!','Yeah!')])
add('defeat',[
('EQ','เชี่ย... ทำไมมันเก่งขนาดนี้วะ...','Damn… how is he this strong…'),
('Rung','ลุกไม่ไหวแล้วว่ะ...','I can’t get up…'),
('Thrower Boss','หมดฤทธิ์แล้วเหรอ?','Out of strength already?'),
('EQ','ครีม... พี่ยังช่วยไม่ได้เลย...','Cream… I haven’t saved you yet…')])
add('awakening',[
('Mysterious Voice','จงตื่นขึ้น... ผู้สืบทอดทั้งสอง','Awaken… both heirs.'),
('EQ','นี่มัน... พลังอะไรวะ!?','What… what is this power!?'),
('Rung','เฮ้ย! แล้วไม้ในมือกูมาจากไหนเนี่ย!?','Hey! Where did this wand come from!?'),
('EQ','เอาไว้ถามทีหลัง! ไปช่วยครีมก่อน!','Questions later! Let’s save Cream first!'),
('Rung','จัดไป!','Let’s do it!')])
add('rescue',[
('EQ','ครีม!','Cream!'),('Cream','พี่อีคิว!','EQ!'),
('EQ','เป็นอะไรไหม!?','Are you hurt!?'),
('Cream','ไม่เป็นไร... หนูกลัวมากเลย','I’m okay… I was so scared.'),
('Rung','ปลอดภัยแล้ว กลับบ้านกันเถอะ','You’re safe now. Let’s go home.'),
('Cream','ขอบคุณนะพี่รุ่ง','Thank you, Rung.')])
add('return-present',[
('EQ','ผมจำได้แล้ว... เรื่องครีม...','I remember… what happened to Cream…'),
('Rung','แต่เงาคนนั้น... มันกำลังทำอะไรกับพวกผมครับ?','But that shadow… what was he doing to us?'),
('Prapot','นั่นคือเวทมนตร์ที่ลบความทรงจำของพวกเธอเมื่อ 16 ปีก่อน','That was the magic used to erase your memories sixteen years ago.'),
('EQ','ใครครับอาจารย์?','Who was it, professor?'),
('Prapot','ชัยนัย... คนที่พวกเธอเพิ่งเจอที่โรงเรียน','Chanai… the man you just met at the school.'),
('Rung','แล้วมันทำไปทำไมครับ?','Why did he do it?'),
('Prapot','เหตุผลของเขายังเป็นปริศนา... เราต้องค้นหาความจริงต่อไป','His reason remains a mystery… we must keep searching for the truth.'),
('Prapot','พลังที่พวกเธอเคยได้รับในอดีต กำลังกลับมา','The powers you received in the past are returning.'),
('EQ','แล้วครีมอยู่ที่ไหนตอนนี้ครับ?','Where is Cream now?'),
('Prapot','คำตอบนั้น... พวกเธอต้องค้นหาด้วยตัวเอง','That answer… you must discover for yourselves.'),
('Rung','แล้วพวกเด็กที่กลายเป็นผีล่ะครับ?','What about the students who became ghosts?'),
('Prapot','พวกเขายังมีโอกาสกลับมาเป็นเหมือนเดิม','There is still a chance to restore them.'),
('Prapot','จงใช้พลังของพวกเธอช่วยเด็กเหล่านั้น ก่อนที่ทุกอย่างจะสายเกินไป','Use your powers to save those students before it is too late.'),
('EQ','เข้าใจแล้วครับอาจารย์','Understood, professor.'),
('Rung','งั้นก็ลุยกันอีกรอบ!','Then let’s go again!')])
add('hub-mystery',[
('Prapot','ชัยนัยจงใจลบความทรงจำของพวกเธอ แต่เหตุผลยังไม่กระจ่าง','Chanai deliberately erased your memories, but his reason is still unknown.'),
('Prapot','ช่วยเหล่านักเรียน แล้วตามหาร่องรอยของเวทมนตร์นั้น','Save the students, and look for traces of that magic.')])
(ROOT/'Assets/EQ_Rung_BeatEmUp/Story').mkdir(parents=True,exist_ok=True)
(ROOT/'Assets/EQ_Rung_BeatEmUp/Story/DialogueSeed.json').write_text(json.dumps(dict(conversations=conversations),ensure_ascii=False,indent=2),encoding='utf-8')
