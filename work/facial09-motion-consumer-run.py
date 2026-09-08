"""Serial CPU runner. Every child is bounded to 55s; no native engine is started."""
import argparse
import json
from pathlib import Path
import subprocess
import sys
import time
import hashlib

HERE = Path(__file__).resolve().parent


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def write(path, value):
    Path(path).write_text(json.dumps(value, indent=2, ensure_ascii=False), encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--export-index", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--part-start", type=int, default=0)
    parser.add_argument("--part-count", type=int, default=0)
    parser.add_argument("--human-chunk", type=int, default=9)
    parser.add_argument("--mosquito-chunk", type=int, default=21)
    args = parser.parse_args()
    output = Path(args.output).resolve()
    if output.exists() and any(output.iterdir()):
        parser.error("output directory must be empty; preserve prior evidence")
    if min(args.human_chunk, args.mosquito_chunk) < 1 or min(args.part_start, args.part_count) < 0:
        parser.error("positive chunks/nonnegative spans required")
    output.mkdir(parents=True, exist_ok=True)
    export = Path(args.export_index).resolve()
    index = json.loads(export.read_text(encoding="utf-8-sig"))
    if not index.get("complete") or index.get("failures"):
        parser.error("native exporter index must be complete first")
    analyzer = HERE/"facial09-motion-consumer.py"
    manifest = {"format":"LMS_FACIAL09_CONSUMER_RUN","complete":False,
                "export_index":str(export),"export_sha256":sha(export),"analyzer_sha256":sha(analyzer),
                "reports":[],"failures":[],"limits":["Numeric boundary-contact analysis only; no visual approval."]}
    manifest_path = output/"runner-index.json"
    def save():
        write(manifest_path,manifest)
    def run(arguments, label, report_path):
        stdout, stderr = output/(label+".stdout.log"), output/(label+".stderr.log")
        watch = time.monotonic()
        entry = {"label":label,"report":str(report_path),"exit_code":None,"verified":False,
                 "stdout":str(stdout),"stderr":str(stderr)}
        manifest["reports"].append(entry)
        save()
        with stdout.open("w",encoding="utf-8") as out, stderr.open("w",encoding="utf-8") as err:
            process = subprocess.run([sys.executable,str(analyzer),*arguments],
                                     stdout=out,stderr=err,timeout=55,
                                     creationflags=getattr(subprocess,"CREATE_NO_WINDOW",0))
        entry["exit_code"], entry["seconds"] = process.returncode,time.monotonic()-watch
        if process.returncode != 0 or stderr.stat().st_size:
            raise RuntimeError(f"{label}: exit={process.returncode}, stderr_bytes={stderr.stat().st_size}")
        data = json.loads(report_path.read_text(encoding="utf-8"))
        if not data.get("current_span_complete"):
            raise RuntimeError(label+": current span not complete")
        entry.update(verified=True,sha256=sha(report_path),case_ids=[c["id"] for c in data["cases"]],
                     unexpected_contacts=data["unexpected_contacts"])
        save()
        print(f"FACIAL09_CONSUMER_PART_OK {label} cases={len(entry['case_ids'])} unexpected={entry['unexpected_contacts']} seconds={entry['seconds']:.2f}",flush=True)
    try:
        parts = index["parts"][args.part_start:args.part_start+args.part_count if args.part_count else None]
        for part in parts:
            if not part.get("verified") or sha(part["file"]) != part["sha256"].lower():
                raise RuntimeError("native part unverified/changed: "+part["file"])
            chunk = args.human_chunk if part["role"]=="human" else args.mosquito_chunk
            for start in range(0,part["count"],chunk):
                count = min(chunk,part["count"]-start)
                label = part["label"]+f"-local-{start:03d}-{start+count-1:03d}"
                report_path = output/(label+".json")
                run(["--input",part["file"],"--case-start",str(start),"--case-count",str(count),
                     "--seconds","45","--output",str(report_path)],label,report_path)
        if sha(analyzer) != manifest["analyzer_sha256"] or sha(export) != manifest["export_sha256"]:
            raise RuntimeError("analyzer or native index changed during run")
        manifest["complete"]=True
        manifest["case_count"]=sum(len(r["case_ids"]) for r in manifest["reports"])
        manifest["unexpected_contacts"]=sum(r["unexpected_contacts"] for r in manifest["reports"])
        # Merge only after all native parts were consumed. Partial runner spans
        # are valid CPU work units but cannot stand for complete285.
        if args.part_start==0 and len(parts)==len(index["parts"]):
            merged=output/"consumer-index.json"
            command=[sys.executable,str(analyzer),"--merge",*[r["report"] for r in manifest["reports"]],
                     "--export-index",str(export),"--output",str(merged)]
            with (output/"merge.stdout.log").open("w") as out,(output/"merge.stderr.log").open("w") as err:
                process=subprocess.run(command,stdout=out,stderr=err,timeout=55,
                                       creationflags=getattr(subprocess,"CREATE_NO_WINDOW",0))
            manifest["merge_exit_code"]=process.returncode
            if process.returncode!=0 or (output/"merge.stderr.log").stat().st_size:
                raise RuntimeError("complete285 merge rejected; inspect merge logs")
            manifest["merged_index"]={"path":str(merged),"sha256":sha(merged)}
        save()
        print(f"FACIAL09_CONSUMER_RUN_OK cases={manifest['case_count']} unexpected={manifest['unexpected_contacts']} output={output}",flush=True)
        return 0
    except (RuntimeError,subprocess.TimeoutExpired) as error:
        manifest["complete"]=False
        manifest["failures"].append(str(error))
        save()
        print("FACIAL09_CONSUMER_RUN_FAIL "+str(error),flush=True)
        return 2


if __name__=="__main__":
    raise SystemExit(main())
