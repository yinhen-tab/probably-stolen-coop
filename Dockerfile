# Probably Stolen 联机 mod — 权威服务端(WebSocket)
FROM python:3.12-slim

WORKDIR /app

COPY server/requirements.txt /app/requirements.txt
RUN pip install --no-cache-dir -r /app/requirements.txt

COPY server/ /app/

ENV COOP_HOST=0.0.0.0
ENV COOP_PORT=27500

EXPOSE 27500

CMD ["python", "server.py"]
