"""Build a searchable, paginated local gallery for extracted UMO textures."""

import argparse
import csv
import json
from pathlib import Path
import re
import shutil


HTML = r'''<!doctype html>
<html lang="ko">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>UMO 이미지 검토 카탈로그</title>
<style>
:root { color-scheme: dark; --thumb: 220px; }
* { box-sizing: border-box; }
body { margin: 0; font-family: "Malgun Gothic", sans-serif; background:#10131a; color:#edf2ff; }
header { position:sticky; top:0; z-index:10; padding:14px 18px; background:#171d29ee; border-bottom:1px solid #38445b; backdrop-filter:blur(8px); }
h1 { margin:0 0 10px; font-size:22px; }
.controls { display:flex; flex-wrap:wrap; gap:8px; align-items:center; }
input,select,button { min-height:38px; border:1px solid #53627d; border-radius:7px; background:#252d3b; color:#fff; padding:7px 10px; font-size:15px; }
#query { width:min(480px,90vw); }
button { cursor:pointer; }
button:hover { background:#344158; }
.summary { margin-top:9px; color:#b9c8e8; }
main { padding:15px; }
.grid { display:grid; grid-template-columns:repeat(auto-fill,minmax(var(--thumb),1fr)); gap:12px; }
.card { min-width:0; border:1px solid #38445b; border-radius:9px; background:#1b2230; overflow:hidden; }
.image-box { height:var(--thumb); display:flex; align-items:center; justify-content:center; background-color:#fff; background-image:linear-gradient(45deg,#ddd 25%,transparent 25%),linear-gradient(-45deg,#ddd 25%,transparent 25%),linear-gradient(45deg,transparent 75%,#ddd 75%),linear-gradient(-45deg,transparent 75%,#ddd 75%); background-size:20px 20px; background-position:0 0,0 10px,10px -10px,-10px 0; }
.image-box img { display:block; max-width:100%; max-height:100%; image-rendering:auto; cursor:zoom-in; }
.meta { padding:9px; font-size:12px; line-height:1.45; overflow-wrap:anywhere; }
.name { font-weight:bold; font-size:13px; color:#fff; }
.bundle { color:#9fb4dd; }
.candidate { color:#78e7a1; }
.mark { display:flex; gap:6px; align-items:center; margin-top:6px; color:#ffd66b; }
.mark input { min-height:auto; }
.pager { display:flex; justify-content:center; gap:10px; align-items:center; margin:18px; }
.hidden { display:none; }
</style>
</head>
<body>
<header>
  <h1>UMO 이미지 검토 카탈로그</h1>
  <div class="controls">
    <input id="query" placeholder="번들 경로 또는 텍스처 이름 검색">
    <select id="root"></select>
    <select id="mode"><option value="review">UI 검토 후보</option><option value="all">전체 이미지</option><option value="selected">내가 표시한 이미지</option></select>
    <label>썸네일 <input id="size" type="range" min="120" max="600" step="20" value="220"></label>
    <select id="perPage"><option>60</option><option selected>120</option><option>240</option><option>480</option></select>
    <button id="export">표시 목록 TSV 저장</button>
  </div>
  <div class="summary" id="summary"></div>
</header>
<main><div class="grid" id="grid"></div><div class="pager"><button id="prev">이전</button><span id="page"></span><button id="next">다음</button></div></main>
<script>
const DATA=__DATA__;
const ROOT_COUNTS=__ROOT_COUNTS__;
const selected=new Set(JSON.parse(localStorage.getItem('umo-image-selection')||'[]'));
let filtered=[], page=0;
const $=id=>document.getElementById(id);
const root=$('root');
root.innerHTML='<option value="">모든 경로</option>'+Object.entries(ROOT_COUNTS).sort((a,b)=>a[0].localeCompare(b[0])).map(([k,v])=>`<option value="${k}">${k} (${v.toLocaleString()})</option>`).join('');
function saveSelection(){ localStorage.setItem('umo-image-selection',JSON.stringify([...selected])); }
function apply(){
  const q=$('query').value.trim().toLowerCase(), r=root.value, mode=$('mode').value;
  filtered=DATA.filter(x=>(!r||x[0]===r)&&(!q||(x[1]+' '+x[2]).toLowerCase().includes(q))&&(mode==='all'||(mode==='review'&&x[6])||(mode==='selected'&&selected.has(x[3]))));
  page=0; render();
}
function render(){
  const per=+$('perPage').value, pages=Math.max(1,Math.ceil(filtered.length/per)); page=Math.max(0,Math.min(page,pages-1));
  const items=filtered.slice(page*per,(page+1)*per), grid=$('grid'); grid.textContent='';
  for(const x of items){
    const card=document.createElement('article'); card.className='card';
    const box=document.createElement('div'); box.className='image-box';
    const img=document.createElement('img'); img.loading='lazy'; img.src=x[3]; img.alt=x[2]; img.title='클릭하면 원본 크기로 엽니다'; img.onclick=()=>window.open(x[3],'_blank'); box.appendChild(img);
    const meta=document.createElement('div'); meta.className='meta';
    const name=document.createElement('div'); name.className='name'; name.textContent=x[2];
    const bundle=document.createElement('div'); bundle.className='bundle'; bundle.textContent=x[1];
    const dims=document.createElement('div'); dims.textContent=`${x[4]}×${x[5]} · Texture2D ${x[7]}`;
    const why=document.createElement('div'); why.className='candidate'; why.textContent=x[6] ? `검토 후보 · ${x[8]}` : '';
    const label=document.createElement('label'); label.className='mark'; const check=document.createElement('input'); check.type='checkbox'; check.checked=selected.has(x[3]);
    check.onchange=()=>{ check.checked?selected.add(x[3]):selected.delete(x[3]); saveSelection(); $('summary').textContent=`검색 결과 ${filtered.length.toLocaleString()}개 · 표시 ${selected.size.toLocaleString()}개`; };
    label.append(check,document.createTextNode('번역 대상으로 표시'));
    meta.append(name,bundle,dims,why,label); card.append(box,meta); grid.appendChild(card);
  }
  $('summary').textContent=`검색 결과 ${filtered.length.toLocaleString()}개 · 전체 ${DATA.length.toLocaleString()}개 · 표시 ${selected.size.toLocaleString()}개`;
  $('page').textContent=`${page+1} / ${pages}`; $('prev').disabled=page===0; $('next').disabled=page>=pages-1;
  window.scrollTo({top:0,behavior:'instant'});
}
$('query').oninput=apply; root.onchange=apply; $('mode').onchange=apply; $('perPage').onchange=apply;
$('size').oninput=e=>document.documentElement.style.setProperty('--thumb',e.target.value+'px');
$('prev').onclick=()=>{page--;render()}; $('next').onclick=()=>{page++;render()};
$('export').onclick=()=>{
  const rows=['bundle\ttexture_name\twidth\theight\timage_path'];
  for(const x of DATA) if(selected.has(x[3])) rows.push([x[1],x[2],x[4],x[5],x[3]].join('\t'));
  const blob=new Blob(['\ufeff'+rows.join('\r\n')],{type:'text/tab-separated-values;charset=utf-8'}), a=document.createElement('a'); a.href=URL.createObjectURL(blob); a.download='UMO_번역대상_선택목록.tsv'; a.click(); URL.revokeObjectURL(a.href);
};
apply();
</script>
</body></html>'''


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    root = Path(__file__).resolve().parents[2]
    parser.add_argument("--extraction-root", type=Path, default=root / "outputs/umo_image_extraction_20260907")
    args = parser.parse_args()
    extraction = args.extraction_root.resolve()
    manifest = extraction / "Manifests/images.jsonl"
    ui_root = extraction / "UI_Candidates"
    catalog = []
    candidates = []
    root_counts = {}

    for line in manifest.open(encoding="utf-8"):
        item = json.loads(line)
        source_root = item["bundle"].split("/", 1)[0]
        # ly contains the actual screen atlases despite its legacy path name.
        review_candidate = bool(item["ui_candidate"]) or source_root == "ly"
        reasons = list(item.get("ui_reasons", []))
        if source_root == "ly" and not item["ui_candidate"]:
            reasons.append("ly 화면·문자 아틀라스 경로")
            destination_relative = Path("UI_Candidates") / Path(item["all_images_path"]).relative_to("All_Images")
            destination = extraction / destination_relative
            if not destination.exists():
                destination.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(extraction / item["all_images_path"], destination)
        if review_candidate:
            candidates.append({
                "bundle": item["bundle"],
                "texture_name": item["texture_name"],
                "width": item["width"],
                "height": item["height"],
                "all_images_path": item["all_images_path"],
                "reasons": reasons,
            })
        root_counts[source_root] = root_counts.get(source_root, 0) + 1
        catalog.append([
            source_root,
            item["bundle"],
            item["texture_name"],
            item["all_images_path"],
            item["width"],
            item["height"],
            review_candidate,
            item["path_id"],
            " · ".join(reasons),
        ])

    output = extraction / "UMO_이미지_검토_카탈로그.html"
    page = HTML.replace("__DATA__", json.dumps(catalog, ensure_ascii=False, separators=(",", ":")))
    page = page.replace("__ROOT_COUNTS__", json.dumps(root_counts, ensure_ascii=False, separators=(",", ":")))
    output.write_text(page, encoding="utf-8")

    candidate_manifest = extraction / "Manifests/review_candidates.csv"
    with candidate_manifest.open("w", encoding="utf-8-sig", newline="") as target:
        writer = csv.DictWriter(target, fieldnames=["bundle", "texture_name", "width", "height", "all_images_path", "reasons"])
        writer.writeheader()
        for item in candidates:
            row = dict(item)
            row["reasons"] = " | ".join(row["reasons"])
            writer.writerow(row)
    summary_path = extraction / "summary.json"
    if summary_path.is_file():
        summary = json.loads(summary_path.read_text(encoding="utf-8"))
        summary["review_candidates"] = len(candidates)
        summary["catalog"] = output.as_posix()
        summary["review_candidates_csv"] = candidate_manifest.as_posix()
        summary_path.write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
    guide_path = extraction / "읽어주세요.md"
    if guide_path.is_file():
        guide = guide_path.read_text(encoding="utf-8")
        guide = re.sub(
            r"- UI 후보 사본: [^\n]+",
            f"- 1차 자동 UI 후보: {sum(bool(x[6]) and x[0] != 'ly' for x in catalog):,}개\n"
            f"- `ly` 화면 아틀라스를 포함한 최종 검토 후보: {len(candidates):,}개",
            guide,
        )
        if "UMO_이미지_검토_카탈로그.html" not in guide:
            guide += "\n## 편리하게 찾는 방법\n\n"
            guide += "- `UMO_이미지_검토_카탈로그.html`을 더블클릭하면 전체 이미지를 검색·필터링할 수 있습니다.\n"
            guide += "- `ly`를 선택하면 화면 문자와 메뉴 아틀라스 788개만 모아서 볼 수 있습니다.\n"
            guide += "- 필요한 이미지의 `번역 대상으로 표시`를 체크한 뒤 TSV 목록으로 저장할 수 있습니다.\n"
        guide_path.write_text(guide, encoding="utf-8")
    print(json.dumps({"catalog": str(output), "images": len(catalog), "review_candidates": len(candidates), "candidate_manifest": str(candidate_manifest)}, ensure_ascii=False))


if __name__ == "__main__":
    main()
