package com.trivium.orderworker;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.springframework.amqp.rabbit.annotation.RabbitListener;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.http.client.JdkClientHttpRequestFactory;
import org.springframework.stereotype.Component;
import org.springframework.web.client.RestTemplate;

import java.net.http.HttpClient;
import java.util.Map;

@Component
public class OrderConsumer {

    private final ObjectMapper mapper = new ObjectMapper();
    private final RestTemplate rest = new RestTemplate(
        new JdkClientHttpRequestFactory(HttpClient.newHttpClient()));

    @Value("${order-worker.order-api-url}")
    private String orderApiUrl;

    @Value("${order-worker.notifications-url}")
    private String notificationsUrl;

    @RabbitListener(queues = "orders")
    public void onOrder(String message) {
        try {
            JsonNode order = mapper.readTree(message);
            JsonNode idNode = order.get("id");
            if (idNode == null) {
                idNode = order.get("Id");
            }
            if (idNode == null || !idNode.canConvertToInt()) {
                throw new IllegalArgumentException("Order message does not contain a valid id");
            }
            int id = idNode.asInt();

            updateStatus(id, "Processing");
            notify(id, "Processing");

            Thread.sleep(1500); // simulate work: pack, ship, etc.

            updateStatus(id, "Shipped");
            notify(id, "Shipped");
        } catch (Exception e) {
            System.err.println("[order-worker] failed to process message: " + e.getMessage());
        }
    }

    private void updateStatus(int id, String status) {
        rest.patchForObject(orderApiUrl + "/orders/{id}/status", Map.of("status", status), Void.class, id);
    }

    private void notify(int id, String status) {
        rest.postForObject(notificationsUrl + "/notify", Map.of("orderId", id, "status", status), Void.class);
    }
}
