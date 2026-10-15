# Probably Stolen 联机 mod — 权威服务端(WebSocket)
FROM python:3.12-slim

WORKDIR /app

COPY server/requirements.txt /app/requirements.txt
RUN pip install --no-cache-dir -i https://pypi.tuna.tsinghua.edu.cn/simple -r /app/requirements.txt

COPY server/ /app/

ENV COOP_HOST=0.0.0.0
ENV COOP_PORT=27500
# 日志等级: quiet / normal(默认) / debug(逐条转发流水,排查用)
ENV COOP_LOG_LEVEL=normal

EXPOSE 27500

CMD ["python", "server.py"]
